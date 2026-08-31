namespace SlackBotBroker.Executors;

public sealed record ExecutorDisconnectContext
{
    public string? Reason { get; init; }
}
