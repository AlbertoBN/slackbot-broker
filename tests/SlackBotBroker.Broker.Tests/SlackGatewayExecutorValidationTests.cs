namespace SlackBotBroker.Broker.Tests;

public class SlackGatewayExecutorValidationTests
{
    [Fact]
    public async Task Unknown_executor_key_is_rejected_before_dispatch()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 1);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(executorKey: "not-configured"), CancellationToken.None);

        var sent = Assert.Single(slackClient.SentMessages);
        Assert.Contains("Unknown executor", sent.Text);
        Assert.True(scheduler.TryAdmit(TestRequests.EchoRequest()));
    }

    [Fact]
    public async Task Disallowed_operation_for_a_known_executor_is_rejected_before_dispatch()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 1);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(operation: "delete-everything"), CancellationToken.None);

        var sent = Assert.Single(slackClient.SentMessages);
        Assert.Contains("is not allowed", sent.Text);
        Assert.True(scheduler.TryAdmit(TestRequests.EchoRequest()));
    }
}
