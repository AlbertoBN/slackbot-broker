using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SlackBotBroker.Broker;

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Worker:SocketPath"] = Path.Combine(Path.GetTempPath(), "slackbot-broker", "worker.sock"),
        ["Broker:QueueCapacity"] = "8",
        ["SlackClient:Dev:SeedCommand:Enabled"] = "false",
    })
    .AddEnvironmentVariables()
    .Build();

var workerSocketPath = configuration["Worker:SocketPath"]!;
var queueCapacity = configuration.GetValue<int>("Broker:QueueCapacity");
var seedCommandEnabled = configuration.GetValue<bool>("SlackClient:Dev:SeedCommand:Enabled");

var policy = new SlackGatewayPolicy
{
    AuthorizedUserIds = ["dev-user"],
    Executors = new Dictionary<string, ExecutorPolicy>
    {
        ["mock"] = new ExecutorPolicy { AllowedOperations = ["echo"] },
    },
};

SlackCommand? seedCommand = seedCommandEnabled
    ? new SlackCommand
    {
        UserId = "dev-user",
        ChannelId = "dev-channel",
        ThreadTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
        ExecutorKey = "mock",
        Operation = "echo",
        Payload = JsonSerializer.SerializeToElement(new { }),
    }
    : null;

var connection = new IpcWorkerConnection(workerSocketPath);
var scheduler = new ExecutionScheduler(queueCapacity);
var slackClient = new ConsoleSlackClient(seedCommand);
var gateway = new SlackGateway(slackClient, scheduler, connection, policy);
connection.EventListener = gateway;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Broker connecting to worker at {workerSocketPath}");

var connectionTask = connection.RunAsync(cts.Token);
var schedulerTask = scheduler.RunAsync(connection.SubmitAsync, cts.Token);
var gatewayTask = gateway.RunAsync(cts.Token);

try
{
    await Task.WhenAll(connectionTask, schedulerTask, gatewayTask);
}
catch (OperationCanceledException)
{
}

Console.WriteLine("Broker stopped.");
