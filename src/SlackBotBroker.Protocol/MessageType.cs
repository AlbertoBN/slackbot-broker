using System.Text.Json.Serialization;

namespace SlackBotBroker.Protocol;

[JsonConverter(typeof(JsonStringEnumConverter<MessageType>))]
public enum MessageType
{
    ExecutionRequest,
    ExecutionAccepted,
    ExecutionProgress,
    ExecutionCompleted,
    ExecutionFailed,
    ExecutionCancelled,
    CancelExecution,
    ExecutorStatusRequest,
    ExecutorStatusResponse,
    ConnectExecutor,
    ConnectExecutorResult,
    DisconnectExecutor,
    DisconnectExecutorResult,
    HealthPing,
    HealthPong,
}
