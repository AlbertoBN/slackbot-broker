using SlackBotBroker.Executors;
using SlackBotBroker.Protocol.Payloads;
using SlackBotBroker.Worker;

namespace SlackBotBroker.Worker.Tests;

public class ExecutionDispatcherResolutionTests
{
    [Fact]
    public async Task Unregistered_executor_key_fails_without_invoking_any_executor()
    {
        var mock = new MockExecutor("mock") { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Hang } };
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = mock }]);
        var dispatcher = new ExecutionDispatcher(registry);
        var sink = new RecordingExecutionEventSink();
        var request = TestRequests.EchoRequest(executorKey: "not-registered");

        await dispatcher.DispatchAsync(request, sink, CancellationToken.None);

        var failed = Assert.Single(sink.Events);
        var failedPayload = Assert.IsType<ExecutionFailedPayload>(failed);
        Assert.Equal(request.RequestId, failedPayload.RequestId);
        Assert.Equal(ExecutorLifecycleState.Disconnected, mock.State); // never touched
    }

    [Fact]
    public async Task Registered_executor_is_resolved_before_any_other_processing()
    {
        var mock = new MockExecutor("mock") { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Succeed } };
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = mock }]);
        var dispatcher = new ExecutionDispatcher(registry);
        var sink = new RecordingExecutionEventSink();
        var request = TestRequests.EchoRequest(executorKey: "mock");

        await dispatcher.DispatchAsync(request, sink, CancellationToken.None);

        Assert.Contains(sink.Events, e => e is ExecutionAcceptedPayload);
        Assert.Contains(sink.Events, e => e is ExecutionCompletedPayload);
    }
}
