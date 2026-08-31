using System.Collections.Concurrent;
using SlackBotBroker.Executors;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Worker;

public sealed class ExecutionDispatcher(IExecutorRegistry registry) : IExecutionDispatcher
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _inFlight = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _targetGates = new();

    public async Task DispatchAsync(ExecutionRequestPayload request, IExecutionEventSink eventSink, CancellationToken cancellationToken)
    {
        if (!registry.TryGet(request.ExecutorKey, out var executor))
        {
            await eventSink.FailedAsync(
                Failed(request.RequestId, $"executor '{request.ExecutorKey}' is not registered", "ExecutorUnavailable"),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!executor.Capabilities.SupportedOperations.Contains(request.Operation))
        {
            await eventSink.FailedAsync(
                Failed(request.RequestId, $"operation '{request.Operation}' is not supported by executor '{request.ExecutorKey}'", "UnsupportedOperation"),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await eventSink.AcceptedAsync(
            new ExecutionAcceptedPayload { RequestId = request.RequestId, AcceptedAtUtc = DateTimeOffset.UtcNow },
            cancellationToken).ConfigureAwait(false);

        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _inFlight[request.RequestId] = executionCts;

        var gate = request.TargetAlias is { Length: > 0 } alias ? _targetGates.GetOrAdd(alias, _ => new SemaphoreSlim(1, 1)) : null;

        try
        {
            if (gate is not null)
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var progress = new BlockingAsyncProgress<ExecutorProgress>(step =>
                    eventSink.ProgressAsync(ToProgressPayload(request.RequestId, step), cancellationToken));

                var messageContext = new ExecutorMessageContext
                {
                    RequestId = request.RequestId,
                    Operation = request.Operation,
                    Payload = request.Payload,
                    TargetAlias = request.TargetAlias,
                    Timeout = request.TimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
                };

                var result = await executor.MessageAsync(messageContext, progress, executionCts.Token).ConfigureAwait(false);

                await ForwardTerminalAsync(eventSink, request.RequestId, result, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate?.Release();
            }
        }
        finally
        {
            _inFlight.TryRemove(request.RequestId, out _);
        }
    }

    public bool TryCancel(Guid requestId, string? reason)
    {
        if (!_inFlight.TryGetValue(requestId, out var cts))
        {
            return false;
        }

        cts.Cancel();
        return true;
    }

    private static Task ForwardTerminalAsync(IExecutionEventSink sink, Guid requestId, ExecutorMessageResult result, CancellationToken cancellationToken) =>
        result.Status switch
        {
            ExecutorOutcomeStatus.Success => sink.CompletedAsync(
                new ExecutionCompletedPayload
                {
                    RequestId = requestId,
                    Summary = result.Summary,
                    Detail = result.Detail,
                    StructuredOutput = result.StructuredOutput,
                    Artifacts = result.Artifacts,
                    ExitCodeOrStatus = result.ExitCodeOrStatus,
                    Duration = result.Duration,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken),

            ExecutorOutcomeStatus.Cancelled => sink.CancelledAsync(
                new ExecutionCancelledPayload
                {
                    RequestId = requestId,
                    Reason = result.Summary,
                    CancelledAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken),

            _ => sink.FailedAsync(Failed(requestId, result.Summary, result.Status.ToString(), result.Detail, result.Duration), cancellationToken),
        };

    private static ExecutionFailedPayload Failed(Guid requestId, string summary, string category, string? detail = null, TimeSpan? duration = null) =>
        new()
        {
            RequestId = requestId,
            Summary = summary,
            Detail = detail ?? summary,
            FailureCategory = category,
            Duration = duration,
            FailedAtUtc = DateTimeOffset.UtcNow,
        };

    private static ExecutionProgressPayload ToProgressPayload(Guid requestId, ExecutorProgress progress) =>
        new()
        {
            RequestId = requestId,
            Status = progress.Status,
            Message = progress.Message,
            Stage = progress.Stage,
            PercentComplete = progress.PercentComplete,
            Detail = progress.Detail,
            Timestamp = progress.Timestamp,
        };
}
