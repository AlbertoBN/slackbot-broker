namespace SlackBotBroker.Protocol.Ipc;

/// <summary>
/// Tracks whether a <c>HealthPong</c> arrived within the configured window after a
/// <c>HealthPing</c> was sent, so a broken IPC session can be detected without waiting on a
/// concrete transport. The caller supplies the current time so tests are deterministic.
/// </summary>
public sealed class HealthLivenessMonitor(TimeSpan window)
{
    private DateTimeOffset? _pingSentAtUtc;

    /// <summary>Call when a <c>HealthPing</c> is sent.</summary>
    public void OnPingSent(DateTimeOffset nowUtc) => _pingSentAtUtc = nowUtc;

    /// <summary>Call when the matching <c>HealthPong</c> is received.</summary>
    public void OnPongReceived() => _pingSentAtUtc = null;

    /// <summary>True once a ping has been sent and no matching pong has arrived within <see cref="window"/>.</summary>
    public bool IsSessionBroken(DateTimeOffset nowUtc) =>
        _pingSentAtUtc is { } sentAtUtc && (nowUtc - sentAtUtc) > window;
}
