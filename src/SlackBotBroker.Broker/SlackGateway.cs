using System.Collections.Concurrent;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Broker;

/// <summary>
/// Validates and admits Slack commands, and routes worker lifecycle events back to the Slack
/// channel/thread each request came from.
/// </summary>
public sealed class SlackGateway : IWorkerEventListener
{
    private readonly ISlackClient _slackClient;
    private readonly ExecutionScheduler _scheduler;
    private readonly IWorkerConnectionState _workerConnection;
    private readonly SlackGatewayPolicy _policy;
    private readonly SensitiveContentRedactor _redactor;
    private readonly ConcurrentDictionary<Guid, (string ChannelId, string ThreadTs)> _routes = new();
    private readonly ConcurrentDictionary<Guid, SlackCommand> _pendingConfirmations = new();

    public SlackGateway(ISlackClient slackClient, ExecutionScheduler scheduler, IWorkerConnectionState workerConnection, SlackGatewayPolicy policy)
    {
        _slackClient = slackClient;
        _scheduler = scheduler;
        _workerConnection = workerConnection;
        _policy = policy;
        _redactor = new SensitiveContentRedactor(policy.SensitiveContentPatterns);
    }

    /// <summary>Consumes commands from <see cref="ISlackClient.ReceiveCommandsAsync"/> until cancelled.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var command in _slackClient.ReceiveCommandsAsync(cancellationToken).ConfigureAwait(false))
        {
            await HandleCommandAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task HandleCommandAsync(SlackCommand command, CancellationToken cancellationToken)
    {
        if (command.ConfirmsRequestId is { } confirmsRequestId)
        {
            await HandleConfirmationAsync(confirmsRequestId, command, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!_policy.AuthorizedUserIds.Contains(command.UserId))
        {
            await SendAsync(command.ChannelId, command.ThreadTs, "You are not authorized to run commands.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command.ExecutorKey is null || !_policy.Executors.TryGetValue(command.ExecutorKey, out var executorPolicy))
        {
            await SendAsync(command.ChannelId, command.ThreadTs, $"Unknown executor '{command.ExecutorKey}'.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command.Operation is null || !executorPolicy.AllowedOperations.Contains(command.Operation))
        {
            await SendAsync(command.ChannelId, command.ThreadTs, $"Operation '{command.Operation}' is not allowed for executor '{command.ExecutorKey}'.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command.TargetAlias is { Length: > 0 } alias && !_policy.AllowedTargetAliases.Contains(alias))
        {
            await SendAsync(command.ChannelId, command.ThreadTs, $"Unknown target alias '{alias}'.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var requestId = Guid.NewGuid();

        if (executorPolicy.HighImpactOperations.Contains(command.Operation))
        {
            _pendingConfirmations[requestId] = command;
            await SendAsync(
                command.ChannelId,
                command.ThreadTs,
                $"'{command.Operation}' on '{command.ExecutorKey}' is a high-impact action. Reply to confirm (confirmation id: {requestId}).",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await AdmitAsync(requestId, command, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleConfirmationAsync(Guid requestId, SlackCommand confirmation, CancellationToken cancellationToken)
    {
        if (!_pendingConfirmations.TryRemove(requestId, out var pendingCommand))
        {
            await SendAsync(confirmation.ChannelId, confirmation.ThreadTs, "No pending confirmation found for that id.", cancellationToken).ConfigureAwait(false);
            return;
        }

        await AdmitAsync(requestId, pendingCommand, cancellationToken).ConfigureAwait(false);
    }

    private async Task AdmitAsync(Guid requestId, SlackCommand command, CancellationToken cancellationToken)
    {
        _routes[requestId] = (command.ChannelId, command.ThreadTs);

        if (!_workerConnection.IsConnected)
        {
            await SendAsync(command.ChannelId, command.ThreadTs, "The local worker is currently unavailable. Please try again shortly.", cancellationToken).ConfigureAwait(false);
            _routes.TryRemove(requestId, out _);
            return;
        }

        var request = new ExecutionRequestPayload
        {
            RequestId = requestId,
            CorrelationId = requestId,
            SlackChannelId = command.ChannelId,
            SlackThreadTs = command.ThreadTs,
            RequestedByUserId = command.UserId,
            ExecutorKey = command.ExecutorKey!,
            Operation = command.Operation!,
            PayloadVersion = 1,
            Payload = command.Payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TargetAlias = command.TargetAlias,
            TimeoutSeconds = command.TimeoutSeconds,
        };

        if (!_scheduler.TryAdmit(request))
        {
            await SendAsync(command.ChannelId, command.ThreadTs, "The system is busy right now. Please try again shortly.", cancellationToken).ConfigureAwait(false);
            _routes.TryRemove(requestId, out _);
        }
    }

    public Task AcceptedAsync(ExecutionAcceptedPayload accepted, CancellationToken cancellationToken) =>
        RouteAsync(accepted.RequestId, "Request accepted.", cancellationToken);

    public Task ProgressAsync(ExecutionProgressPayload progress, CancellationToken cancellationToken) =>
        RouteAsync(progress.RequestId, progress.Message, cancellationToken);

    public Task CompletedAsync(ExecutionCompletedPayload completed, CancellationToken cancellationToken) =>
        RouteTerminalAsync(completed.RequestId, completed.Summary, cancellationToken);

    public Task FailedAsync(ExecutionFailedPayload failed, CancellationToken cancellationToken) =>
        RouteTerminalAsync(failed.RequestId, $"Failed: {failed.Summary}", cancellationToken);

    public Task CancelledAsync(ExecutionCancelledPayload cancelled, CancellationToken cancellationToken) =>
        RouteTerminalAsync(cancelled.RequestId, "Cancelled.", cancellationToken);

    private async Task RouteAsync(Guid requestId, string text, CancellationToken cancellationToken)
    {
        if (_routes.TryGetValue(requestId, out var route))
        {
            await SendAsync(route.ChannelId, route.ThreadTs, text, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RouteTerminalAsync(Guid requestId, string text, CancellationToken cancellationToken)
    {
        await RouteAsync(requestId, text, cancellationToken).ConfigureAwait(false);
        _routes.TryRemove(requestId, out _);
    }

    private Task SendAsync(string channelId, string threadTs, string text, CancellationToken cancellationToken) =>
        _slackClient.SendMessageAsync(channelId, threadTs, _redactor.Redact(text), cancellationToken);
}
