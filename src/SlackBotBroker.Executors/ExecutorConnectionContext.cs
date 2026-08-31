namespace SlackBotBroker.Executors;

public sealed record ExecutorConnectionContext
{
    public required Guid RequestId { get; init; }
    public string? Environment { get; init; }
    public string? CallerIdentity { get; init; }
    public string? ConfigurationReference { get; init; }
}
