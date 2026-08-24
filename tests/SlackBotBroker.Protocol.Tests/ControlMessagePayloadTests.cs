using System.Text.Json;
using SlackBotBroker.Protocol;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Protocol.Tests;

public class ControlMessagePayloadTests
{
    [Fact]
    public void CancelExecution_round_trips()
    {
        var requestId = Guid.NewGuid();
        var payload = new CancelExecutionPayload { RequestId = requestId, Reason = "user requested" };

        var envelope = EnvelopeCodec.Create(requestId, Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetCancelExecutionPayload();

        Assert.Equal(MessageType.CancelExecution, envelope.MessageType);
        Assert.Equal(payload.RequestId, roundTripped.RequestId);
        Assert.Equal(payload.Reason, roundTripped.Reason);
    }

    [Fact]
    public void ExecutorStatusRequest_round_trips()
    {
        var payload = new ExecutorStatusRequestPayload { ExecutorKey = "claude-code" };

        var envelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetExecutorStatusRequestPayload();

        Assert.Equal(MessageType.ExecutorStatusRequest, envelope.MessageType);
        Assert.Equal(payload.ExecutorKey, roundTripped.ExecutorKey);
    }

    [Fact]
    public void ExecutorStatusRequest_null_key_means_all_executors()
    {
        var payload = new ExecutorStatusRequestPayload();

        var envelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetExecutorStatusRequestPayload();

        Assert.Null(roundTripped.ExecutorKey);
    }

    [Fact]
    public void ExecutorStatusResponse_round_trips_multiple_executors()
    {
        var capabilities = JsonSerializer.SerializeToElement(new { supportsProgress = true });
        var payload = new ExecutorStatusResponsePayload
        {
            Executors =
            [
                new ExecutorStatusEntry { ExecutorKey = "claude-code", State = "Ready", Capabilities = capabilities },
                new ExecutorStatusEntry { ExecutorKey = "git", State = "Busy" },
            ],
        };

        var envelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetExecutorStatusResponsePayload();

        Assert.Equal(MessageType.ExecutorStatusResponse, envelope.MessageType);
        Assert.Equal(2, roundTripped.Executors.Count);
        Assert.Equal("claude-code", roundTripped.Executors[0].ExecutorKey);
        Assert.Equal("Ready", roundTripped.Executors[0].State);
        Assert.Equal(capabilities.GetRawText(), roundTripped.Executors[0].Capabilities!.Value.GetRawText());
        Assert.Equal("git", roundTripped.Executors[1].ExecutorKey);
        Assert.Equal("Busy", roundTripped.Executors[1].State);
        Assert.Null(roundTripped.Executors[1].Capabilities);
    }

    [Fact]
    public void ConnectExecutor_round_trips()
    {
        var payload = new ConnectExecutorPayload { ExecutorKey = "claude-code" };

        var envelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetConnectExecutorPayload();

        Assert.Equal(MessageType.ConnectExecutor, envelope.MessageType);
        Assert.Equal(payload.ExecutorKey, roundTripped.ExecutorKey);
    }

    [Fact]
    public void ConnectExecutorResult_round_trips_success_and_failure()
    {
        var success = new ConnectExecutorResultPayload { ExecutorKey = "claude-code", Success = true, State = "Ready" };
        var failure = new ConnectExecutorResultPayload { ExecutorKey = "unknown", Success = false, FailureReason = "not configured" };

        var successEnvelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, success);
        var failureEnvelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, failure);

        var roundTrippedSuccess = successEnvelope.GetConnectExecutorResultPayload();
        var roundTrippedFailure = failureEnvelope.GetConnectExecutorResultPayload();

        Assert.True(roundTrippedSuccess.Success);
        Assert.Equal("Ready", roundTrippedSuccess.State);
        Assert.False(roundTrippedFailure.Success);
        Assert.Equal("not configured", roundTrippedFailure.FailureReason);
    }

    [Fact]
    public void DisconnectExecutor_round_trips()
    {
        var payload = new DisconnectExecutorPayload { ExecutorKey = "claude-code", Reason = "shutdown" };

        var envelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetDisconnectExecutorPayload();

        Assert.Equal(MessageType.DisconnectExecutor, envelope.MessageType);
        Assert.Equal(payload.ExecutorKey, roundTripped.ExecutorKey);
        Assert.Equal(payload.Reason, roundTripped.Reason);
    }

    [Fact]
    public void DisconnectExecutorResult_round_trips()
    {
        var payload = new DisconnectExecutorResultPayload { ExecutorKey = "claude-code", Success = true, State = "Disconnected" };

        var envelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetDisconnectExecutorResultPayload();

        Assert.Equal(MessageType.DisconnectExecutorResult, envelope.MessageType);
        Assert.True(roundTripped.Success);
        Assert.Equal("Disconnected", roundTripped.State);
    }

    [Fact]
    public void HealthPing_and_HealthPong_round_trip()
    {
        var pingEnvelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, new HealthPingPayload());
        var pongEnvelope = EnvelopeCodec.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, new HealthPongPayload());

        Assert.Equal(MessageType.HealthPing, pingEnvelope.MessageType);
        Assert.Equal(MessageType.HealthPong, pongEnvelope.MessageType);
        Assert.NotNull(pingEnvelope.GetHealthPingPayload());
        Assert.NotNull(pongEnvelope.GetHealthPongPayload());
    }
}
