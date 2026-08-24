using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlackBotBroker.Protocol;

public sealed record MessageEnvelope
{
    [JsonPropertyName("messageType")]
    public required MessageType MessageType { get; init; }

    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }

    [JsonPropertyName("requestId")]
    public required Guid RequestId { get; init; }

    [JsonPropertyName("correlationId")]
    public required Guid CorrelationId { get; init; }

    [JsonPropertyName("sentAtUtc")]
    public required DateTimeOffset SentAtUtc { get; init; }

    [JsonPropertyName("payload")]
    public required JsonElement Payload { get; init; }
}
