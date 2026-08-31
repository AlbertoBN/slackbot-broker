using SlackBotBroker.Broker;
using SlackBotBroker.Executors;
using SlackBotBroker.Worker;

namespace SlackBotBroker.IntegrationTests;

/// <summary>
/// Wires a real worker host and a real broker-side IPC client together over a temp-path Unix
/// domain socket, driven through <see cref="MockExecutor"/> and a fake Slack client — no real
/// Slack connection, but everything else (socket, framing, dispatch, scheduling, gateway) is the
/// genuine production wiring from `src/`.
/// </summary>
public sealed class IntegrationHarness : IAsyncDisposable
{
    public const string AuthorizedUser = "U1";
    public const string ExecutorKey = "mock";
    public const string Operation = "echo";

    private readonly CancellationTokenSource _rootCts = new();
    private readonly CancellationTokenSource _workerCts;
    private readonly CancellationTokenSource _brokerCts;

    private Task? _workerTask;
    private Task? _connectionTask;
    private Task? _schedulerTask;

    public string SocketPath { get; }
    public MockExecutor MockExecutor { get; }
    public IExecutionDispatcher Dispatcher { get; }
    public WorkerIpcServer WorkerServer { get; }
    public IpcWorkerConnection Connection { get; }
    public ExecutionScheduler Scheduler { get; }
    public FakeSlackClient SlackClient { get; }
    public SlackGateway Gateway { get; }

    public IntegrationHarness(int queueCapacity = 4)
    {
        SocketPath = Path.Combine(Path.GetTempPath(), $"sbb-it-{Guid.NewGuid():N}.sock");

        _workerCts = CancellationTokenSource.CreateLinkedTokenSource(_rootCts.Token);
        _brokerCts = CancellationTokenSource.CreateLinkedTokenSource(_rootCts.Token);

        MockExecutor = new MockExecutor(ExecutorKey);
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = MockExecutor }]);
        Dispatcher = new ExecutionDispatcher(registry);
        WorkerServer = new WorkerIpcServer(SocketPath, Dispatcher);

        Connection = new IpcWorkerConnection(SocketPath, healthPingInterval: TimeSpan.FromMilliseconds(100), healthPongWindow: TimeSpan.FromSeconds(2));
        Scheduler = new ExecutionScheduler(queueCapacity);
        SlackClient = new FakeSlackClient();
        var policy = new SlackGatewayPolicy
        {
            AuthorizedUserIds = [AuthorizedUser],
            Executors = new Dictionary<string, ExecutorPolicy>
            {
                [ExecutorKey] = new ExecutorPolicy { AllowedOperations = [Operation] },
            },
        };
        Gateway = new SlackGateway(SlackClient, Scheduler, Connection, policy);
        Connection.EventListener = Gateway;
    }

    public void StartWorker() => _workerTask = WorkerServer.RunAsync(_workerCts.Token);

    public void StartConnection() => _connectionTask = Connection.RunAsync(_brokerCts.Token);

    /// <summary>Starts the scheduler's consumer loop. Deliberately separate from <see cref="StartConnection"/> so a test can fill the queue via <see cref="Scheduler"/> and know it will stay filled (nothing draining it) before exercising rejection behavior.</summary>
    public void StartScheduler() => _schedulerTask = Scheduler.RunAsync(Connection.SubmitAsync, _brokerCts.Token);

    public void StartBroker()
    {
        StartConnection();
        StartScheduler();
    }

    public async Task WaitUntilConnectedAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!Connection.IsConnected)
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Stops only the worker side, simulating the worker becoming unavailable while the broker keeps running.</summary>
    public async Task StopWorkerAsync()
    {
        await _workerCts.CancelAsync();
        if (_workerTask is not null)
        {
            try
            {
                await _workerTask.ConfigureAwait(false);
            }
            catch
            {
                // expected on cancellation
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _rootCts.CancelAsync();

        foreach (var task in new[] { _workerTask, _connectionTask, _schedulerTask })
        {
            if (task is null)
            {
                continue;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // expected on cancellation
            }
        }

        _rootCts.Dispose();
        _workerCts.Dispose();
        _brokerCts.Dispose();

        if (File.Exists(SocketPath))
        {
            File.Delete(SocketPath);
        }
    }
}
