namespace SlackBotBroker.Protocol.Payloads;

public sealed record ExecutionProgressPayload
{
    public required Guid RequestId { get; init; }
    public required string Status { get; init; }
    public required string Message { get; init; }
    public string? Stage { get; init; }
    public int? PercentComplete { get; init; }
    public string? Detail { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
