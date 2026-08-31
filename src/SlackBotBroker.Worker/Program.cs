using Microsoft.Extensions.Configuration;
using SlackBotBroker.Executors;
using SlackBotBroker.Worker;

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Worker:SocketPath"] = Path.Combine(Path.GetTempPath(), "slackbot-broker", "worker.sock"),
        ["Executors:Mock:Enabled"] = "false",
    })
    .AddEnvironmentVariables()
    .Build();

var socketPath = configuration["Worker:SocketPath"]!;
var mockExecutorEnabled = configuration.GetValue<bool>("Executors:Mock:Enabled");

var registry = ExecutorRegistryFactory.Create([], includeMockExecutor: mockExecutorEnabled);
var dispatcher = new ExecutionDispatcher(registry);
var server = new WorkerIpcServer(socketPath, dispatcher);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Worker listening on {socketPath} (MockExecutor enabled: {mockExecutorEnabled})");

try
{
    await server.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
}

Console.WriteLine("Worker stopped.");
