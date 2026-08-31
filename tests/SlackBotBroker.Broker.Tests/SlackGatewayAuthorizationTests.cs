namespace SlackBotBroker.Broker.Tests;

public class SlackGatewayAuthorizationTests
{
    [Fact]
    public async Task Unauthorized_users_command_is_rejected_and_no_execution_request_is_created()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 1);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(userId: TestPolicies.UnauthorizedUser), CancellationToken.None);

        var sent = Assert.Single(slackClient.SentMessages);
        Assert.Contains("not authorized", sent.Text);

        // Capacity-1 queue still has room: the unauthorized command never reached admission.
        Assert.True(scheduler.TryAdmit(TestRequests.EchoRequest()));
    }

    [Fact]
    public async Task Authorized_users_command_is_not_rejected_for_authorization_reasons()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(userId: TestPolicies.AuthorizedUser), CancellationToken.None);

        Assert.DoesNotContain(slackClient.SentMessages, m => m.Text.Contains("not authorized"));
    }
}
