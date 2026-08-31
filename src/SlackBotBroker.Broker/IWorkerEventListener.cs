using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Broker;

/// <summary>
/// The broker-side counterpart to the worker's <c>IExecutionEventSink</c>: receives the
/// lifecycle events the worker reports (over IPC in production, directly in tests) so they can
/// be routed back to Slack.
/// </summary>
public interface IWorkerEventListener
{
    Task AcceptedAsync(ExecutionAcceptedPayload accepted, CancellationToken cancellationToken);

    Task ProgressAsync(ExecutionProgressPayload progress, CancellationToken cancellationToken);

    Task CompletedAsync(ExecutionCompletedPayload completed, CancellationToken cancellationToken);

    Task FailedAsync(ExecutionFailedPayload failed, CancellationToken cancellationToken);

    Task CancelledAsync(ExecutionCancelledPayload cancelled, CancellationToken cancellationToken);
}
