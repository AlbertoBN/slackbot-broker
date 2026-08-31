using SlackBotBroker.Executors;
using SlackBotBroker.Protocol.Payloads;
using SlackBotBroker.Worker;

namespace SlackBotBroker.Worker.Tests;

public class ExecutionDispatcherAdmissionTests
{
    [Fact]
    public async Task Valid_request_is_accepted_before_the_executor_is_invoked()
    {
        var mock = new MockExecutor("mock") { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Succeed } };
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = mock }]);
        var dispatcher = new ExecutionDispatcher(registry);
        var sink = new RecordingExecutionEventSink();
        var request = TestRequests.EchoRequest();

        await dispatcher.DispatchAsync(request, sink, CancellationToken.None);

        Assert.IsType<ExecutionAcceptedPayload>(sink.Events[0]);
        Assert.IsType<ExecutionCompletedPayload>(sink.Events[^1]);
    }

    [Fact]
    public async Task Request_for_an_unsupported_operation_is_failed_without_an_accepted_event()
    {
        var mock = new MockExecutor("mock"); // default capabilities only support "echo"
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = mock }]);
        var dispatcher = new ExecutionDispatcher(registry);
        var sink = new RecordingExecutionEventSink();
        var request = TestRequests.EchoRequest(operation: "not-supported");

        await dispatcher.DispatchAsync(request, sink, CancellationToken.None);

        var failed = Assert.Single(sink.Events);
        Assert.IsType<ExecutionFailedPayload>(failed);
        Assert.DoesNotContain(sink.Events, e => e is ExecutionAcceptedPayload);
    }
}
