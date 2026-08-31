using SlackBotBroker.Executors;

namespace SlackBotBroker.Executors.Tests;

public class ExecutorLifecycleStateTests
{
    /// <summary>Minimal <see cref="IExecutor"/> double whose <see cref="State"/> the test drives directly, to exercise every documented lifecycle state without depending on any specific executor's internal behavior.</summary>
    private sealed class RecordingExecutor : IExecutor
    {
        public string ExecutorKey => "recording";
        public ExecutorCapabilities Capabilities { get; } = new() { SupportedOperations = [] };
        public ExecutorLifecycleState State { get; set; } = ExecutorLifecycleState.Disconnected;

        public Task<ExecutorConnectionResult> ConnectAsync(ExecutorConnectionContext context, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task DisconnectAsync(ExecutorDisconnectContext context, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ExecutorMessageResult> MessageAsync(ExecutorMessageContext context, IProgress<ExecutorProgress>? progress, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    [Theory]
    [InlineData(ExecutorLifecycleState.Disconnected)]
    [InlineData(ExecutorLifecycleState.Connecting)]
    [InlineData(ExecutorLifecycleState.Ready)]
    [InlineData(ExecutorLifecycleState.Busy)]
    [InlineData(ExecutorLifecycleState.Degraded)]
    [InlineData(ExecutorLifecycleState.Faulted)]
    [InlineData(ExecutorLifecycleState.Disconnecting)]
    public void Executor_can_report_every_documented_lifecycle_state(ExecutorLifecycleState state)
    {
        var executor = new RecordingExecutor { State = state };

        Assert.Equal(state, executor.State);
    }

    [Fact]
    public void Executor_transitions_through_the_full_documented_sequence()
    {
        var executor = new RecordingExecutor();
        var observed = new List<ExecutorLifecycleState> { executor.State };

        foreach (var next in new[]
                 {
                     ExecutorLifecycleState.Connecting,
                     ExecutorLifecycleState.Ready,
                     ExecutorLifecycleState.Busy,
                     ExecutorLifecycleState.Ready,
                     ExecutorLifecycleState.Degraded,
                     ExecutorLifecycleState.Faulted,
                     ExecutorLifecycleState.Disconnecting,
                     ExecutorLifecycleState.Disconnected,
                 })
        {
            executor.State = next;
            observed.Add(executor.State);
        }

        Assert.Equal(
            [
                ExecutorLifecycleState.Disconnected,
                ExecutorLifecycleState.Connecting,
                ExecutorLifecycleState.Ready,
                ExecutorLifecycleState.Busy,
                ExecutorLifecycleState.Ready,
                ExecutorLifecycleState.Degraded,
                ExecutorLifecycleState.Faulted,
                ExecutorLifecycleState.Disconnecting,
                ExecutorLifecycleState.Disconnected,
            ],
            observed);
    }
}
