using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Worker;

/// <summary>Coordinates executor resolution, lifecycle, progress forwarding, and cancellation for one execution request, without coupling to the IPC transport.</summary>
public interface IExecutionDispatcher
{
    Task DispatchAsync(ExecutionRequestPayload request, IExecutionEventSink eventSink, CancellationToken cancellationToken);

    /// <summary>Requests cancellation of an in-flight execution. A no-op if <paramref name="requestId"/> is unknown or already terminal.</summary>
    /// <returns>True if a cancellation signal was sent to an in-flight execution; false otherwise.</returns>
    bool TryCancel(Guid requestId, string? reason);
}
