using SlackBotBroker.Executors;

namespace SlackBotBroker.IntegrationTests;

public class CancellationIntegrationTests
{
    [Fact]
    public async Task Cancelling_a_hung_execution_reaches_the_fake_slack_client_as_cancelled()
    {
        await using var harness = new IntegrationHarness();
        harness.MockExecutor.Script = new MockExecutorScript { Outcome = MockExecutorOutcome.Hang };
        harness.StartWorker();
        harness.StartBroker();
        await harness.WaitUntilConnectedAsync(TimeSpan.FromSeconds(5));

        var command = TestCommands.Echo(channelId: "C-CANCEL", threadTs: "7.1");
        var requestId = await harness.Gateway.HandleCommandAsync(command, CancellationToken.None);
        Assert.NotNull(requestId);

        await TestWait.UntilAsync(
            () => harness.SlackClient.SentMessages.Any(m => m.ChannelId == "C-CANCEL" && m.Text == "Request accepted."),
            TimeSpan.FromSeconds(5));

        await harness.Connection.RequestCancellationAsync(requestId.Value, "integration test cancel", CancellationToken.None);

        await TestWait.UntilAsync(
            () => harness.SlackClient.SentMessages.Any(m => m.ChannelId == "C-CANCEL" && m.Text == "Cancelled."),
            TimeSpan.FromSeconds(5));

        Assert.DoesNotContain(harness.SlackClient.SentMessages, m => m.ChannelId == "C-CANCEL" && (m.Text.StartsWith("Failed") || m.Text.Contains("completed")));
    }
}
