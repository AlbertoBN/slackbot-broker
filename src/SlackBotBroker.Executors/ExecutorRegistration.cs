namespace SlackBotBroker.Executors;

public sealed record ExecutorRegistration
{
    public required IExecutor Executor { get; init; }
    public bool Enabled { get; init; } = true;
}
