using SlackBotBroker.Executors;
using SlackBotBroker.Worker;

namespace SlackBotBroker.Worker.Tests;

/// <summary>
/// The worker exposes status by resolving an executor through <see cref="IExecutorRegistry"/> and
/// reading its <see cref="IExecutor.State"/>/<see cref="IExecutor.Capabilities"/> directly — no
/// separate status-tracking component is needed, since those properties already reflect the
/// executor's current state independent of any single dispatch call.
/// </summary>
public class ExecutorStatusReportingTests
{
    [Fact]
    public async Task Status_reflects_Busy_while_a_message_is_in_flight_and_recovers_after_cancellation()
    {
        var mock = new MockExecutor("mock") { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Hang } };
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = mock }]);
        var dispatcher = new ExecutionDispatcher(registry);
        var sink = new RecordingExecutionEventSink();
        var request = TestRequests.EchoRequest();

        Assert.True(registry.TryGet("mock", out var beforeDispatch));
        Assert.Equal(ExecutorLifecycleState.Disconnected, beforeDispatch.State);

        var dispatchTask = dispatcher.DispatchAsync(request, sink, CancellationToken.None);

        ExecutorLifecycleState observed = default;
        for (var i = 0; i < 100; i++)
        {
            registry.TryGet("mock", out var duringDispatch);
            observed = duringDispatch!.State;
            if (observed == ExecutorLifecycleState.Busy)
            {
                break;
            }

            await Task.Delay(5);
        }

        Assert.Equal(ExecutorLifecycleState.Busy, observed);

        dispatcher.TryCancel(request.RequestId, "cleanup");
        await dispatchTask;

        registry.TryGet("mock", out var afterDispatch);
        Assert.Equal(ExecutorLifecycleState.Ready, afterDispatch!.State);
    }

    [Fact]
    public void Capabilities_are_readable_from_the_registry_without_dispatching_anything()
    {
        var mock = new MockExecutor("mock");
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = mock }]);

        Assert.True(registry.TryGet("mock", out var executor));
        Assert.NotEmpty(executor.Capabilities.SupportedOperations);
    }
}
