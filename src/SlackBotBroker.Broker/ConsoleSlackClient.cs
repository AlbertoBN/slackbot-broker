using System.Runtime.CompilerServices;

namespace SlackBotBroker.Broker;

/// <summary>
/// Dev/manual-run <see cref="ISlackClient"/>: logs every sent message to the console and,
/// optionally, seeds one command so the pipeline can be exercised without a real Slack
/// connection. A real Socket Mode client is deferred (see design.md Open Questions) — this
/// stands in until one is added in a future change.
/// </summary>
public sealed class ConsoleSlackClient(SlackCommand? seedCommand) : ISlackClient
{
    public async IAsyncEnumerable<SlackCommand> ReceiveCommandsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (seedCommand is { } command)
        {
            yield return command;
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public Task SendMessageAsync(string channelId, string threadTs, string text, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Slack -> #{channelId} thread {threadTs}] {text}");
        return Task.CompletedTask;
    }
}
