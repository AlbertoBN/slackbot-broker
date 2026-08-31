namespace SlackBotBroker.IntegrationTests;

internal static class TestWait
{
    public static async Task UntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!predicate())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(20, cts.Token).ConfigureAwait(false);
        }
    }
}
