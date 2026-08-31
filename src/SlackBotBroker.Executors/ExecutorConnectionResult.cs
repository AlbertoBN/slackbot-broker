namespace SlackBotBroker.Executors;

public enum ExecutorConnectionStatus
{
    Ready,
    Faulted,
}

public sealed record ExecutorConnectionResult
{
    public required ExecutorConnectionStatus Status { get; init; }
    public string? FailureReason { get; init; }
}
