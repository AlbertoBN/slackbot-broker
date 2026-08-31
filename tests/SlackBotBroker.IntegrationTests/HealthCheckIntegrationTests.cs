namespace SlackBotBroker.IntegrationTests;

public class HealthCheckIntegrationTests
{
    [Fact]
    public async Task HealthPing_HealthPong_round_trip_keeps_the_connection_alive()
    {
        await using var harness = new IntegrationHarness();
        harness.StartWorker();
        harness.StartBroker();

        await harness.WaitUntilConnectedAsync(TimeSpan.FromSeconds(5));

        // The harness pings every 100ms and treats a session as broken if no pong arrives
        // within 2s. Staying connected across a window well past that proves pongs are
        // actually coming back, not just that the initial socket connect succeeded.
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            Assert.True(harness.Connection.IsConnected);
            await Task.Delay(50);
        }
    }
}
