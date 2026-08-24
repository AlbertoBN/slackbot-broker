using System.Text.Json;
using SlackBotBroker.Protocol;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Protocol.Tests;

public class ExecutionLifecyclePayloadTests
{
    [Fact]
    public void ExecutionAccepted_preserves_correlation_and_round_trips()
    {
        var requestId = Guid.NewGuid();
        var payload = new ExecutionAcceptedPayload { RequestId = requestId, AcceptedAtUtc = DateTimeOffset.UtcNow };

        var envelope = EnvelopeCodec.Create(requestId, Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetExecutionAcceptedPayload();

        Assert.Equal(MessageType.ExecutionAccepted, envelope.MessageType);
        Assert.Equal(requestId, envelope.RequestId);
        Assert.Equal(payload.RequestId, roundTripped.RequestId);
        Assert.Equal(payload.AcceptedAtUtc, roundTripped.AcceptedAtUtc);
    }

    [Fact]
    public void ExecutionProgress_preserves_correlation_and_round_trips()
    {
        var requestId = Guid.NewGuid();
        var payload = new ExecutionProgressPayload
        {
            RequestId = requestId,
            Status = "Running",
            Message = "Cloning repository",
            Stage = "clone",
            PercentComplete = 25,
            Detail = "shallow clone",
            Timestamp = DateTimeOffset.UtcNow,
        };

        var envelope = EnvelopeCodec.Create(requestId, Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetExecutionProgressPayload();

        Assert.Equal(MessageType.ExecutionProgress, envelope.MessageType);
        Assert.Equal(requestId, envelope.RequestId);
        Assert.Equal(payload.Status, roundTripped.Status);
        Assert.Equal(payload.Message, roundTripped.Message);
        Assert.Equal(payload.Stage, roundTripped.Stage);
        Assert.Equal(payload.PercentComplete, roundTripped.PercentComplete);
        Assert.Equal(payload.Detail, roundTripped.Detail);
        Assert.Equal(payload.Timestamp, roundTripped.Timestamp);
    }

    [Fact]
    public void ExecutionCompleted_preserves_correlation_and_round_trips()
    {
        var requestId = Guid.NewGuid();
        var structuredOutput = JsonSerializer.SerializeToElement(new { filesChanged = 3 });
        var payload = new ExecutionCompletedPayload
        {
            RequestId = requestId,
            Summary = "Analysis complete",
            Detail = "3 files inspected",
            StructuredOutput = structuredOutput,
            Artifacts = ["report.md"],
            ExitCodeOrStatus = "0",
            Duration = TimeSpan.FromSeconds(12),
            CompletedAtUtc = DateTimeOffset.UtcNow,
        };

        var envelope = EnvelopeCodec.Create(requestId, Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetExecutionCompletedPayload();

        Assert.Equal(MessageType.ExecutionCompleted, envelope.MessageType);
        Assert.Equal(requestId, envelope.RequestId);
        Assert.Equal(payload.Summary, roundTripped.Summary);
        Assert.Equal(payload.Detail, roundTripped.Detail);
        Assert.Equal(structuredOutput.GetRawText(), roundTripped.StructuredOutput!.Value.GetRawText());
        Assert.Equal(payload.Artifacts, roundTripped.Artifacts);
        Assert.Equal(payload.ExitCodeOrStatus, roundTripped.ExitCodeOrStatus);
        Assert.Equal(payload.Duration, roundTripped.Duration);
        Assert.Equal(payload.CompletedAtUtc, roundTripped.CompletedAtUtc);
    }

    [Fact]
    public void ExecutionFailed_preserves_correlation_and_carries_structured_detail()
    {
        var requestId = Guid.NewGuid();
        var payload = new ExecutionFailedPayload
        {
            RequestId = requestId,
            Summary = "Executor unavailable",
            Detail = "connection refused",
            FailureCategory = "ExecutorUnavailable",
            Duration = TimeSpan.FromSeconds(1),
            FailedAtUtc = DateTimeOffset.UtcNow,
        };

        var envelope = EnvelopeCodec.Create(requestId, Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetExecutionFailedPayload();

        Assert.Equal(MessageType.ExecutionFailed, envelope.MessageType);
        Assert.Equal(requestId, envelope.RequestId);
        Assert.Equal(payload.Summary, roundTripped.Summary);
        Assert.Equal(payload.Detail, roundTripped.Detail);
        Assert.Equal(payload.FailureCategory, roundTripped.FailureCategory);
        Assert.Equal(payload.Duration, roundTripped.Duration);
        Assert.Equal(payload.FailedAtUtc, roundTripped.FailedAtUtc);
    }

    [Fact]
    public void ExecutionCancelled_preserves_correlation_and_round_trips()
    {
        var requestId = Guid.NewGuid();
        var payload = new ExecutionCancelledPayload { RequestId = requestId, Reason = "user requested", CancelledAtUtc = DateTimeOffset.UtcNow };

        var envelope = EnvelopeCodec.Create(requestId, Guid.NewGuid(), DateTimeOffset.UtcNow, payload);
        var roundTripped = envelope.GetExecutionCancelledPayload();

        Assert.Equal(MessageType.ExecutionCancelled, envelope.MessageType);
        Assert.Equal(requestId, envelope.RequestId);
        Assert.Equal(payload.Reason, roundTripped.Reason);
        Assert.Equal(payload.CancelledAtUtc, roundTripped.CancelledAtUtc);
    }
}
