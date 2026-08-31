using SlackBotBroker.Protocol.Payloads;
using SlackBotBroker.Worker;

namespace SlackBotBroker.Worker.Tests;

/// <summary>Records every event it receives, in arrival order, so tests can assert both content and ordering without a real IPC transport.</summary>
public sealed class RecordingExecutionEventSink : IExecutionEventSink
{
    public List<object> Events { get; } = [];

    public Task AcceptedAsync(ExecutionAcceptedPayload accepted, CancellationToken cancellationToken)
    {
        Events.Add(accepted);
        return Task.CompletedTask;
    }

    public Task ProgressAsync(ExecutionProgressPayload progress, CancellationToken cancellationToken)
    {
        Events.Add(progress);
        return Task.CompletedTask;
    }

    public Task CompletedAsync(ExecutionCompletedPayload completed, CancellationToken cancellationToken)
    {
        Events.Add(completed);
        return Task.CompletedTask;
    }

    public Task FailedAsync(ExecutionFailedPayload failed, CancellationToken cancellationToken)
    {
        Events.Add(failed);
        return Task.CompletedTask;
    }

    public Task CancelledAsync(ExecutionCancelledPayload cancelled, CancellationToken cancellationToken)
    {
        Events.Add(cancelled);
        return Task.CompletedTask;
    }
}
