using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Worker;

/// <summary>Receives the lifecycle events an <see cref="IExecutionDispatcher"/> produces while handling one request, decoupled from how those events actually reach the broker (real IPC in production, a recording fake in tests).</summary>
public interface IExecutionEventSink
{
    Task AcceptedAsync(ExecutionAcceptedPayload accepted, CancellationToken cancellationToken);

    Task ProgressAsync(ExecutionProgressPayload progress, CancellationToken cancellationToken);

    Task CompletedAsync(ExecutionCompletedPayload completed, CancellationToken cancellationToken);

    Task FailedAsync(ExecutionFailedPayload failed, CancellationToken cancellationToken);

    Task CancelledAsync(ExecutionCancelledPayload cancelled, CancellationToken cancellationToken);
}
