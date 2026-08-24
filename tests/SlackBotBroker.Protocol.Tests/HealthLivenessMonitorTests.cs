using SlackBotBroker.Protocol.Ipc;

namespace SlackBotBroker.Protocol.Tests;

public class HealthLivenessMonitorTests
{
    [Fact]
    public void Session_is_not_broken_before_any_ping_is_sent()
    {
        var monitor = new HealthLivenessMonitor(TimeSpan.FromSeconds(1));

        Assert.False(monitor.IsSessionBroken(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Session_is_not_broken_while_within_the_liveness_window()
    {
        var monitor = new HealthLivenessMonitor(TimeSpan.FromSeconds(1));
        var pingSentAt = DateTimeOffset.UtcNow;

        monitor.OnPingSent(pingSentAt);

        Assert.False(monitor.IsSessionBroken(pingSentAt + TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void Session_is_broken_once_the_pong_window_elapses_without_a_response()
    {
        var monitor = new HealthLivenessMonitor(TimeSpan.FromSeconds(1));
        var pingSentAt = DateTimeOffset.UtcNow;

        monitor.OnPingSent(pingSentAt);

        Assert.True(monitor.IsSessionBroken(pingSentAt + TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void A_pong_received_within_the_window_clears_the_broken_state()
    {
        var monitor = new HealthLivenessMonitor(TimeSpan.FromSeconds(1));
        var pingSentAt = DateTimeOffset.UtcNow;

        monitor.OnPingSent(pingSentAt);
        monitor.OnPongReceived();

        Assert.False(monitor.IsSessionBroken(pingSentAt + TimeSpan.FromSeconds(5)));
    }
}
