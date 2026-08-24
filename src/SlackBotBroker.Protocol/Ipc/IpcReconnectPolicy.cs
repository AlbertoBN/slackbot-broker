namespace SlackBotBroker.Protocol.Ipc;

public sealed record IpcReconnectPolicy
{
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    public double BackoffMultiplier { get; init; } = 2.0;
}
