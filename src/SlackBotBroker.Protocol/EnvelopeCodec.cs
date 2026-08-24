using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Protocol;

/// <summary>Creates envelopes around, and extracts, the protocol's typed payloads using only the source-generated <see cref="ProtocolJsonContext"/> — never reflection-based serialization.</summary>
public static class EnvelopeCodec
{
    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, ExecutionRequestPayload payload) =>
        CreateCore(MessageType.ExecutionRequest, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.ExecutionRequestPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, ExecutionAcceptedPayload payload) =>
        CreateCore(MessageType.ExecutionAccepted, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.ExecutionAcceptedPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, ExecutionProgressPayload payload) =>
        CreateCore(MessageType.ExecutionProgress, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.ExecutionProgressPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, ExecutionCompletedPayload payload) =>
        CreateCore(MessageType.ExecutionCompleted, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.ExecutionCompletedPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, ExecutionFailedPayload payload) =>
        CreateCore(MessageType.ExecutionFailed, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.ExecutionFailedPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, ExecutionCancelledPayload payload) =>
        CreateCore(MessageType.ExecutionCancelled, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.ExecutionCancelledPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, CancelExecutionPayload payload) =>
        CreateCore(MessageType.CancelExecution, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.CancelExecutionPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, ExecutorStatusRequestPayload payload) =>
        CreateCore(MessageType.ExecutorStatusRequest, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.ExecutorStatusRequestPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, ExecutorStatusResponsePayload payload) =>
        CreateCore(MessageType.ExecutorStatusResponse, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.ExecutorStatusResponsePayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, ConnectExecutorPayload payload) =>
        CreateCore(MessageType.ConnectExecutor, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.ConnectExecutorPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, ConnectExecutorResultPayload payload) =>
        CreateCore(MessageType.ConnectExecutorResult, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.ConnectExecutorResultPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, DisconnectExecutorPayload payload) =>
        CreateCore(MessageType.DisconnectExecutor, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.DisconnectExecutorPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, DisconnectExecutorResultPayload payload) =>
        CreateCore(MessageType.DisconnectExecutorResult, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.DisconnectExecutorResultPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, HealthPingPayload payload) =>
        CreateCore(MessageType.HealthPing, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.HealthPingPayload);

    public static MessageEnvelope Create(Guid requestId, Guid correlationId, DateTimeOffset sentAtUtc, HealthPongPayload payload) =>
        CreateCore(MessageType.HealthPong, requestId, correlationId, sentAtUtc, payload, ProtocolJsonContext.Default.HealthPongPayload);

    public static ExecutionRequestPayload GetExecutionRequestPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.ExecutionRequestPayload);

    public static ExecutionAcceptedPayload GetExecutionAcceptedPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.ExecutionAcceptedPayload);

    public static ExecutionProgressPayload GetExecutionProgressPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.ExecutionProgressPayload);

    public static ExecutionCompletedPayload GetExecutionCompletedPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.ExecutionCompletedPayload);

    public static ExecutionFailedPayload GetExecutionFailedPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.ExecutionFailedPayload);

    public static ExecutionCancelledPayload GetExecutionCancelledPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.ExecutionCancelledPayload);

    public static CancelExecutionPayload GetCancelExecutionPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.CancelExecutionPayload);

    public static ExecutorStatusRequestPayload GetExecutorStatusRequestPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.ExecutorStatusRequestPayload);

    public static ExecutorStatusResponsePayload GetExecutorStatusResponsePayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.ExecutorStatusResponsePayload);

    public static ConnectExecutorPayload GetConnectExecutorPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.ConnectExecutorPayload);

    public static ConnectExecutorResultPayload GetConnectExecutorResultPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.ConnectExecutorResultPayload);

    public static DisconnectExecutorPayload GetDisconnectExecutorPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.DisconnectExecutorPayload);

    public static DisconnectExecutorResultPayload GetDisconnectExecutorResultPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.DisconnectExecutorResultPayload);

    public static HealthPingPayload GetHealthPingPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.HealthPingPayload);

    public static HealthPongPayload GetHealthPongPayload(this MessageEnvelope envelope) =>
        GetCore(envelope, ProtocolJsonContext.Default.HealthPongPayload);

    private const int CurrentProtocolVersion = 1;

    private static MessageEnvelope CreateCore<TPayload>(
        MessageType messageType,
        Guid requestId,
        Guid correlationId,
        DateTimeOffset sentAtUtc,
        TPayload payload,
        JsonTypeInfo<TPayload> typeInfo)
    {
        var payloadElement = JsonSerializer.SerializeToElement(payload, typeInfo);
        return new MessageEnvelope
        {
            MessageType = messageType,
            ProtocolVersion = CurrentProtocolVersion,
            RequestId = requestId,
            CorrelationId = correlationId,
            SentAtUtc = sentAtUtc,
            Payload = payloadElement,
        };
    }

    private static TPayload GetCore<TPayload>(MessageEnvelope envelope, JsonTypeInfo<TPayload> typeInfo) =>
        envelope.Payload.Deserialize(typeInfo)
        ?? throw new JsonException($"Envelope payload for {envelope.MessageType} deserialized to null.");
}
