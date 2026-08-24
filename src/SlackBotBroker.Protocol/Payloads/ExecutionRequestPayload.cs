using System.Text.Json;

namespace SlackBotBroker.Protocol.Payloads;

public sealed record ExecutionRequestPayload
{
    public required Guid RequestId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required string SlackChannelId { get; init; }
    public required string SlackThreadTs { get; init; }
    public required string RequestedByUserId { get; init; }
    public required string ExecutorKey { get; init; }
    public required string Operation { get; init; }
    public required int PayloadVersion { get; init; }
    public required JsonElement Payload { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public string? TargetAlias { get; init; }
    public ExecutionMode? Mode { get; init; }
    public int? TimeoutSeconds { get; init; }
}
