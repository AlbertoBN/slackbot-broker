namespace SlackBotBroker.Broker.Tests;

public class SlackGatewaySeamTests
{
    [Fact]
    public async Task Command_received_over_the_seam_reaches_the_gateway_with_no_real_slack_sdk_involved()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        // An unauthorized command makes gateway processing observable: if RunAsync only drained
        // ReceiveCommandsAsync without invoking the handler, no rejection message would appear.
        slackClient.Enqueue(TestPolicies.Command(userId: TestPolicies.UnauthorizedUser));
        slackClient.CompleteCommands();

        await gateway.RunAsync(CancellationToken.None);

        var sent = Assert.Single(slackClient.SentMessages);
        Assert.Contains("not authorized", sent.Text);
    }

    [Fact]
    public async Task RunAsync_stops_once_the_command_stream_completes()
    {
        var slackClient = new FakeSlackClient();
        var scheduler = new ExecutionScheduler(capacity: 4);
        var gateway = new SlackGateway(slackClient, scheduler, new FakeWorkerConnectionState(), TestPolicies.Default());

        slackClient.CompleteCommands();

        var runTask = gateway.RunAsync(CancellationToken.None);

        await runTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
