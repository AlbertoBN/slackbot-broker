namespace SlackBotBroker.IntegrationTests;

public class QueueFullIntegrationTests
{
    [Fact]
    public async Task Next_command_receives_a_busy_response_without_reaching_the_worker()
    {
        await using var harness = new IntegrationHarness(queueCapacity: 1);
        harness.StartWorker();
        harness.StartConnection();
        await harness.WaitUntilConnectedAsync(TimeSpan.FromSeconds(5));

        // Deliberately never call StartScheduler(): the queue fills and stays filled, with
        // nothing draining it, so the next admission attempt deterministically fails.
        Assert.True(harness.Scheduler.TryAdmit(TestRequests.Echo()));

        var command = TestCommands.Echo(channelId: "C-BUSY", threadTs: "9.1");
        await harness.Gateway.HandleCommandAsync(command, CancellationToken.None);

        var sentToChannel = harness.SlackClient.SentMessages.Where(m => m.ChannelId == "C-BUSY").ToList();
        var busyMessage = Assert.Single(sentToChannel);
        Assert.Contains("busy", busyMessage.Text, StringComparison.OrdinalIgnoreCase);

        // Give the worker a moment it doesn't need: confirm it never saw this request.
        await Task.Delay(200);
        Assert.DoesNotContain(harness.SlackClient.SentMessages, m => m.ChannelId == "C-BUSY" && m.Text == "Request accepted.");
    }
}
