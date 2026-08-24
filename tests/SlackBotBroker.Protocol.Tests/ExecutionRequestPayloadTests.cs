using System.Text.Json;
using SlackBotBroker.Protocol;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Protocol.Tests;

public class ExecutionRequestPayloadTests
{
    [Fact]
    public void All_required_fields_round_trip()
    {
        var innerPayload = JsonSerializer.SerializeToElement(new { operationArg = "value" });
        var payload = new ExecutionRequestPayload
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            SlackChannelId = "C123",
            SlackThreadTs = "1710000000.000100",
            RequestedByUserId = "U456",
            ExecutorKey = "claude-code",
            Operation = "analyze",
            PayloadVersion = 1,
            Payload = innerPayload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        var envelope = EnvelopeCodec.Create(payload.RequestId, payload.CorrelationId, DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetExecutionRequestPayload();

        Assert.Equal(payload.RequestId, roundTripped.RequestId);
        Assert.Equal(payload.CorrelationId, roundTripped.CorrelationId);
        Assert.Equal(payload.SlackChannelId, roundTripped.SlackChannelId);
        Assert.Equal(payload.SlackThreadTs, roundTripped.SlackThreadTs);
        Assert.Equal(payload.RequestedByUserId, roundTripped.RequestedByUserId);
        Assert.Equal(payload.ExecutorKey, roundTripped.ExecutorKey);
        Assert.Equal(payload.Operation, roundTripped.Operation);
        Assert.Equal(payload.PayloadVersion, roundTripped.PayloadVersion);
        Assert.Equal(payload.CreatedAtUtc, roundTripped.CreatedAtUtc);
        Assert.Equal(innerPayload.GetRawText(), roundTripped.Payload.GetRawText());
        Assert.Null(roundTripped.TargetAlias);
        Assert.Null(roundTripped.Mode);
        Assert.Null(roundTripped.TimeoutSeconds);
    }

    [Fact]
    public void Optional_fields_round_trip_when_present()
    {
        var payload = new ExecutionRequestPayload
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            SlackChannelId = "C123",
            SlackThreadTs = "1710000000.000100",
            RequestedByUserId = "U456",
            ExecutorKey = "git",
            Operation = "status",
            PayloadVersion = 1,
            Payload = JsonSerializer.SerializeToElement(new { }),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TargetAlias = "my-repo",
            Mode = ExecutionMode.Apply,
            TimeoutSeconds = 120,
        };

        var envelope = EnvelopeCodec.Create(payload.RequestId, payload.CorrelationId, DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetExecutionRequestPayload();

        Assert.Equal("my-repo", roundTripped.TargetAlias);
        Assert.Equal(ExecutionMode.Apply, roundTripped.Mode);
        Assert.Equal(120, roundTripped.TimeoutSeconds);
    }

    [Fact]
    public void Envelope_message_type_is_ExecutionRequest()
    {
        var payload = new ExecutionRequestPayload
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            SlackChannelId = "C123",
            SlackThreadTs = "1710000000.000100",
            RequestedByUserId = "U456",
            ExecutorKey = "git",
            Operation = "status",
            PayloadVersion = 1,
            Payload = JsonSerializer.SerializeToElement(new { }),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        var envelope = EnvelopeCodec.Create(payload.RequestId, payload.CorrelationId, DateTimeOffset.UtcNow, payload);

        Assert.Equal(MessageType.ExecutionRequest, envelope.MessageType);
    }
}
