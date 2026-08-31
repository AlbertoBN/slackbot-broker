namespace SlackBotBroker.IntegrationTests;

public class SuccessPathIntegrationTests
{
    [Fact]
    public async Task Command_flows_end_to_end_through_MockExecutor_to_the_fake_slack_client()
    {
        await using var harness = new IntegrationHarness();
        // MockExecutor's default script (Succeed) is what we want here.
        harness.StartWorker();
        harness.StartBroker();
        await harness.WaitUntilConnectedAsync(TimeSpan.FromSeconds(5));

        var command = TestCommands.Echo(channelId: "C-SUCCESS", threadTs: "42.1");
        var requestId = await harness.Gateway.HandleCommandAsync(command, CancellationToken.None);
        Assert.NotNull(requestId);

        await TestWait.UntilAsync(
            () => harness.SlackClient.SentMessages.Any(m => m.ChannelId == "C-SUCCESS" && m.Text == "mock executor completed"),
            TimeSpan.FromSeconds(5));

        Assert.Contains(harness.SlackClient.SentMessages, m => m.ChannelId == "C-SUCCESS" && m.Text == "Request accepted.");

        var completion = Assert.Single(harness.SlackClient.SentMessages, m => m.ChannelId == "C-SUCCESS" && m.Text == "mock executor completed");
        Assert.Equal("42.1", completion.ThreadTs);
    }
}
