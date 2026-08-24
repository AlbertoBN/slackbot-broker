using System.Text.Json.Serialization;
using SlackBotBroker.Protocol.Payloads;

namespace SlackBotBroker.Protocol;

[JsonSerializable(typeof(MessageEnvelope))]
[JsonSerializable(typeof(ExecutionRequestPayload))]
[JsonSerializable(typeof(ExecutionAcceptedPayload))]
[JsonSerializable(typeof(ExecutionProgressPayload))]
[JsonSerializable(typeof(ExecutionCompletedPayload))]
[JsonSerializable(typeof(ExecutionFailedPayload))]
[JsonSerializable(typeof(ExecutionCancelledPayload))]
[JsonSerializable(typeof(CancelExecutionPayload))]
[JsonSerializable(typeof(ExecutorStatusRequestPayload))]
[JsonSerializable(typeof(ExecutorStatusResponsePayload))]
[JsonSerializable(typeof(ConnectExecutorPayload))]
[JsonSerializable(typeof(ConnectExecutorResultPayload))]
[JsonSerializable(typeof(DisconnectExecutorPayload))]
[JsonSerializable(typeof(DisconnectExecutorResultPayload))]
[JsonSerializable(typeof(HealthPingPayload))]
[JsonSerializable(typeof(HealthPongPayload))]
public sealed partial class ProtocolJsonContext : JsonSerializerContext;
