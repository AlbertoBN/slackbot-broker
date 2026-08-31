namespace SlackBotBroker.Broker.Tests;

public class ExecutionSchedulerAdmissionTests
{
    [Fact]
    public void Request_is_admitted_while_capacity_remains()
    {
        var scheduler = new ExecutionScheduler(capacity: 2);

        var admitted = scheduler.TryAdmit(TestRequests.EchoRequest());

        Assert.True(admitted);
    }

    [Fact]
    public void Request_is_rejected_and_not_enqueued_once_the_queue_is_full()
    {
        var scheduler = new ExecutionScheduler(capacity: 1);
        Assert.True(scheduler.TryAdmit(TestRequests.EchoRequest()));

        var rejected = scheduler.TryAdmit(TestRequests.EchoRequest());

        Assert.False(rejected);
    }
}
