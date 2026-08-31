namespace SlackBotBroker.Broker.Tests;

public sealed class FakeWorkerConnectionState : IWorkerConnectionState
{
    public bool IsConnected { get; set; } = true;
}
