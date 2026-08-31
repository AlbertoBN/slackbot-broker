namespace SlackBotBroker.Executors;

public enum ExecutorLifecycleState
{
    Disconnected,
    Connecting,
    Ready,
    Busy,
    Degraded,
    Faulted,
    Disconnecting,
}
