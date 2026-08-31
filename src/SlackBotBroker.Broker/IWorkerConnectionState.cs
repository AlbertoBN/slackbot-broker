namespace SlackBotBroker.Broker;

/// <summary>Reports whether the broker currently has a live IPC connection to the worker. The real implementation is driven by the IPC client in host wiring; tests use a simple settable fake.</summary>
public interface IWorkerConnectionState
{
    bool IsConnected { get; }
}
