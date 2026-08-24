using System.Text.Json;
using SlackBotBroker.Protocol;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Protocol.Tests;

public class MessageEnvelopeTests
{
    [Fact]
    public void Envelope_round_trips_through_json()
    {
        var requestId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var sentAtUtc = DateTimeOffset.UtcNow;
        var payload = new HealthPingPayload();

        var envelope = EnvelopeCodec.Create(requestId, correlationId, sentAtUtc, payload);

        var json = JsonSerializer.Serialize(envelope, ProtocolJsonContext.Default.MessageEnvelope);
        var roundTripped = JsonSerializer.Deserialize(json, ProtocolJsonContext.Default.MessageEnvelope);

        Assert.NotNull(roundTripped);
        Assert.Equal(MessageType.HealthPing, roundTripped.MessageType);
        Assert.Equal(1, roundTripped.ProtocolVersion);
        Assert.Equal(requestId, roundTripped.RequestId);
        Assert.Equal(correlationId, roundTripped.CorrelationId);
        Assert.Equal(sentAtUtc, roundTripped.SentAtUtc);
    }

    [Fact]
    public void Envelope_uses_camelCase_field_names_on_the_wire()
    {
        var envelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, new HealthPingPayload());

        var json = JsonSerializer.Serialize(envelope, ProtocolJsonContext.Default.MessageEnvelope);

        Assert.Contains("\"messageType\"", json);
        Assert.Contains("\"protocolVersion\"", json);
        Assert.Contains("\"requestId\"", json);
        Assert.Contains("\"correlationId\"", json);
        Assert.Contains("\"sentAtUtc\"", json);
        Assert.Contains("\"payload\"", json);
    }

    [Fact]
    public void Malformed_envelope_missing_a_required_field_is_rejected()
    {
        // No "correlationId" field present.
        var malformedJson = """
            {"messageType":"HealthPing","protocolVersion":1,"requestId":"11111111-1111-1111-1111-111111111111","sentAtUtc":"2026-01-01T00:00:00Z","payload":{}}
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(malformedJson, ProtocolJsonContext.Default.MessageEnvelope));
    }
}
