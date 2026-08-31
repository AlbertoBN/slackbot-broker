using SlackBotBroker.Executors;
using SlackBotBroker.Protocol.Payloads;
using SlackBotBroker.Worker;

namespace SlackBotBroker.Worker.Tests;

public class ExecutionDispatcherCancellationTests
{
    [Fact]
    public async Task Cancelling_an_in_flight_execution_reports_a_cancelled_outcome()
    {
        var mock = new MockExecutor("mock") { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Hang } };
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = mock }]);
        var dispatcher = new ExecutionDispatcher(registry);
        var sink = new RecordingExecutionEventSink();
        var request = TestRequests.EchoRequest();

        var dispatchTask = dispatcher.DispatchAsync(request, sink, CancellationToken.None);

        // Give the dispatch loop a chance to register the in-flight request before cancelling.
        var cancelled = false;
        for (var i = 0; i < 100 && !cancelled; i++)
        {
            cancelled = dispatcher.TryCancel(request.RequestId, "test cancel");
            if (!cancelled)
            {
                await Task.Delay(5);
            }
        }

        Assert.True(cancelled);
        await dispatchTask;

        var terminal = Assert.Single(sink.Events, e => e is ExecutionCancelledPayload);
        Assert.Equal(request.RequestId, ((ExecutionCancelledPayload)terminal).RequestId);
        Assert.DoesNotContain(sink.Events, e => e is ExecutionCompletedPayload or ExecutionFailedPayload);
    }

    [Fact]
    public async Task Cancelling_an_already_terminal_execution_is_a_no_op()
    {
        var mock = new MockExecutor("mock") { Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Succeed } };
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = mock }]);
        var dispatcher = new ExecutionDispatcher(registry);
        var sink = new RecordingExecutionEventSink();
        var request = TestRequests.EchoRequest();

        await dispatcher.DispatchAsync(request, sink, CancellationToken.None);
        var eventCountAfterCompletion = sink.Events.Count;

        var cancelResult = dispatcher.TryCancel(request.RequestId, "too late");

        Assert.False(cancelResult);
        Assert.Equal(eventCountAfterCompletion, sink.Events.Count);
        Assert.Single(sink.Events, e => e is ExecutionCompletedPayload);
    }

    [Fact]
    public void Cancelling_an_unknown_request_id_is_a_no_op()
    {
        var registry = new ExecutorRegistry([]);
        var dispatcher = new ExecutionDispatcher(registry);

        var result = dispatcher.TryCancel(Guid.NewGuid(), "unknown");

        Assert.False(result);
    }
}
