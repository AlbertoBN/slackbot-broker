using System.Text.Json;

namespace SlackBotBroker.Broker;

/// <summary>
/// One inbound Slack interaction, already anchored to its originating channel/thread. When
/// <see cref="ConfirmsRequestId"/> is set, this represents the user's confirmation of a
/// previously prompted high-impact command rather than a new command, and
/// <see cref="ExecutorKey"/>/<see cref="Operation"/> are not required.
/// </summary>
public sealed record SlackCommand
{
    public required string UserId { get; init; }
    public required string ChannelId { get; init; }
    public required string ThreadTs { get; init; }
    public Guid? ConfirmsRequestId { get; init; }
    public string? ExecutorKey { get; init; }
    public string? Operation { get; init; }
    public string? TargetAlias { get; init; }
    public JsonElement Payload { get; init; }
    public int? TimeoutSeconds { get; init; }
}
