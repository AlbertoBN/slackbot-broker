namespace SlackBotBroker.Executors;

public sealed record ExecutorCapabilities
{
    public required IReadOnlyCollection<string> SupportedOperations { get; init; }
    public bool SupportsConnect { get; init; }
    public bool SupportsDisconnect { get; init; }
    public bool SupportsCancellation { get; init; }
    public bool SupportsProgress { get; init; }
    public bool SupportsStreaming { get; init; }
    public bool SupportsConcurrentMessages { get; init; }
    public bool HasManagedLifecycle { get; init; }
    public bool SupportsHealthChecks { get; init; }
}
