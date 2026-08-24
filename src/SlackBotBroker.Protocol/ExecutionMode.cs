using System.Text.Json.Serialization;

namespace SlackBotBroker.Protocol;

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionMode>))]
public enum ExecutionMode
{
    ReadOnly,
    Plan,
    Apply,
}
