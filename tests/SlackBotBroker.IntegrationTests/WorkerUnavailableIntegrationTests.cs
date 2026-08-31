namespace SlackBotBroker.IntegrationTests;

public class WorkerUnavailableIntegrationTests
{
    [Fact]
    public async Task Worker_unavailability_surfaces_a_distinct_message_without_hanging_or_crashing()
    {
        await using var harness = new IntegrationHarness();
        harness.StartWorker();
        harness.StartBroker();
        await harness.WaitUntilConnectedAsync(TimeSpan.FromSeconds(5));

        await harness.StopWorkerAsync();

        await TestWait.UntilAsync(() => !harness.Connection.IsConnected, TimeSpan.FromSeconds(5));

        var command = TestCommands.Echo(channelId: "C-DOWN", threadTs: "3.1");

        // WaitAsync guards against the specific failure mode this scenario cares about: the
        // gateway hanging forever instead of surfacing a message when the worker is gone.
        await harness.Gateway.HandleCommandAsync(command, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        var sent = Assert.Single(harness.SlackClient.SentMessages, m => m.ChannelId == "C-DOWN");
        Assert.Contains("worker", sent.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unavailable", sent.Text, StringComparison.OrdinalIgnoreCase);
    }
}
