namespace SlackBotBroker.Broker.Tests;

public class SlackGatewayAliasValidationTests
{
    [Fact]
    public async Task Raw_path_instead_of_a_configured_alias_is_rejected()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 1);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(targetAlias: "/home/alberto/some/raw/path"), CancellationToken.None);

        var sent = Assert.Single(slackClient.SentMessages);
        Assert.Contains("Unknown target alias", sent.Text);
        Assert.True(scheduler.TryAdmit(TestRequests.EchoRequest()));
    }

    [Fact]
    public async Task Configured_alias_is_accepted()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        await gateway.HandleCommandAsync(TestPolicies.Command(targetAlias: "repo-a"), CancellationToken.None);

        Assert.DoesNotContain(slackClient.SentMessages, m => m.Text.Contains("Unknown target alias"));
    }
}
