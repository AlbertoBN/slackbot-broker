using SlackBotBroker.Executors;

namespace SlackBotBroker.Executors.Tests;

public class MockExecutorConnectDisconnectTests
{
    private static ExecutorConnectionContext ConnectionContext() => new() { RequestId = Guid.NewGuid() };

    [Fact]
    public async Task Connect_is_idempotent_when_already_ready()
    {
        var executor = new MockExecutor();
        await executor.ConnectAsync(ConnectionContext(), CancellationToken.None);
        Assert.Equal(ExecutorLifecycleState.Ready, executor.State);

        var second = await executor.ConnectAsync(ConnectionContext(), CancellationToken.None);

        Assert.Equal(ExecutorConnectionStatus.Ready, second.Status);
        Assert.Equal(ExecutorLifecycleState.Ready, executor.State);
    }

    [Fact]
    public async Task Disconnect_after_a_failed_connect_completes_without_error()
    {
        var executor = new MockExecutor { FailConnect = true };
        var connectResult = await executor.ConnectAsync(ConnectionContext(), CancellationToken.None);
        Assert.Equal(ExecutorConnectionStatus.Faulted, connectResult.Status);

        var exception = await Record.ExceptionAsync(() =>
            executor.DisconnectAsync(new ExecutorDisconnectContext(), CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(ExecutorLifecycleState.Disconnected, executor.State);
    }

    [Fact]
    public async Task Disconnecting_a_managed_executor_stops_the_underlying_process()
    {
        var executor = new MockExecutor { IsExternallyManaged = false };
        await executor.ConnectAsync(ConnectionContext(), CancellationToken.None);

        await executor.DisconnectAsync(new ExecutorDisconnectContext(), CancellationToken.None);

        Assert.True(executor.UnderlyingProcessStopped);
    }

    [Fact]
    public async Task Disconnecting_an_externally_managed_executor_does_not_stop_the_underlying_process()
    {
        var executor = new MockExecutor { IsExternallyManaged = true };
        await executor.ConnectAsync(ConnectionContext(), CancellationToken.None);

        await executor.DisconnectAsync(new ExecutorDisconnectContext(), CancellationToken.None);

        Assert.False(executor.UnderlyingProcessStopped);
        Assert.Equal(ExecutorLifecycleState.Disconnected, executor.State);
    }
}
