namespace SlackBotBroker.Protocol.Payloads;

/// <summary>Queries status for a single executor when <see cref="ExecutorKey"/> is set, or every configured executor when it is null.</summary>
public sealed record ExecutorStatusRequestPayload
{
    public string? ExecutorKey { get; init; }
}
