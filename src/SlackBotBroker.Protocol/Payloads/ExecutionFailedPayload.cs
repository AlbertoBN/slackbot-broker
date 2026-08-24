namespace SlackBotBroker.Protocol.Payloads;

public sealed record ExecutionFailedPayload
{
    public required Guid RequestId { get; init; }
    public required string Summary { get; init; }
    public required string Detail { get; init; }
    public required string FailureCategory { get; init; }
    public TimeSpan? Duration { get; init; }
    public required DateTimeOffset FailedAtUtc { get; init; }
}
