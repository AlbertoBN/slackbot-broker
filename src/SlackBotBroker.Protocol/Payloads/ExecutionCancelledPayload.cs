namespace SlackBotBroker.Protocol.Payloads;

public sealed record ExecutionCancelledPayload
{
    public required Guid RequestId { get; init; }
    public string? Reason { get; init; }
    public required DateTimeOffset CancelledAtUtc { get; init; }
}
