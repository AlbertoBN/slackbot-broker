namespace SlackBotBroker.Broker;

public sealed record ExecutorPolicy
{
    public required IReadOnlyCollection<string> AllowedOperations { get; init; }

    /// <summary>Operations within <see cref="AllowedOperations"/> that require explicit Slack confirmation before dispatch.</summary>
    public IReadOnlyCollection<string> HighImpactOperations { get; init; } = [];
}

public sealed record SlackGatewayPolicy
{
    public required IReadOnlyCollection<string> AuthorizedUserIds { get; init; }
    public required IReadOnlyDictionary<string, ExecutorPolicy> Executors { get; init; }
    public IReadOnlyCollection<string> AllowedTargetAliases { get; init; } = [];

    /// <summary>Regex patterns for content that must be redacted before it is posted to Slack.</summary>
    public IReadOnlyCollection<string> SensitiveContentPatterns { get; init; } = [];
}
