using SlackBotBroker.Executors;
using SlackBotBroker.Protocol.Payloads;
using SlackBotBroker.Worker;

namespace SlackBotBroker.Worker.Tests;

public class ExecutionDispatcherEventForwardingTests
{
    [Fact]
    public async Task Progress_events_are_forwarded_in_order_and_the_terminal_event_exactly_once()
    {
        var now = DateTimeOffset.UtcNow;
        var steps = new[]
        {
            new ExecutorProgress { Status = "Running", Message = "step 1", Timestamp = now },
            new ExecutorProgress { Status = "Running", Message = "step 2", Timestamp = now.AddSeconds(1) },
            new ExecutorProgress { Status = "Running", Message = "step 3", Timestamp = now.AddSeconds(2) },
        };
        var mock = new MockExecutor("mock")
        {
            Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Succeed, ProgressSequence = steps },
        };
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = mock }]);
        var dispatcher = new ExecutionDispatcher(registry);
        var sink = new RecordingExecutionEventSink();
        var request = TestRequests.EchoRequest();

        await dispatcher.DispatchAsync(request, sink, CancellationToken.None);

        // Accepted, then 3 progress events in order, then exactly one terminal event.
        Assert.Equal(5, sink.Events.Count);
        Assert.IsType<ExecutionAcceptedPayload>(sink.Events[0]);

        var observedProgress = sink.Events.Skip(1).Take(3).Cast<ExecutionProgressPayload>().Select(p => p.Message).ToArray();
        Assert.Equal(["step 1", "step 2", "step 3"], observedProgress);
        Assert.All(sink.Events.Skip(1).Take(3), e => Assert.Equal(request.RequestId, ((ExecutionProgressPayload)e).RequestId));

        var terminalEvents = sink.Events.Where(e => e is ExecutionCompletedPayload or ExecutionFailedPayload or ExecutionCancelledPayload).ToList();
        var terminal = Assert.Single(terminalEvents);
        Assert.IsType<ExecutionCompletedPayload>(terminal);
    }
}
