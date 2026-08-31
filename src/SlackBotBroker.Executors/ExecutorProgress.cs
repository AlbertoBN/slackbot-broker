namespace SlackBotBroker.Executors;

public sealed record ExecutorProgress
{
    public required string Status { get; init; }
    public required string Message { get; init; }
    public string? Stage { get; init; }
    public int? PercentComplete { get; init; }
    public string? Detail { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
