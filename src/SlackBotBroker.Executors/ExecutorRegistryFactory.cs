namespace SlackBotBroker.Executors;

/// <summary>
/// Builds an <see cref="IExecutorRegistry"/>. <see cref="MockExecutor"/> is included only when
/// <paramref name="includeMockExecutor"/> is true, so it never reaches a default/production
/// registry — hosts should drive that flag from their own <c>Executors:Mock:Enabled</c>
/// configuration.
/// </summary>
public static class ExecutorRegistryFactory
{
    public static IExecutorRegistry Create(IEnumerable<ExecutorRegistration> registrations, bool includeMockExecutor)
    {
        var all = registrations.ToList();
        if (includeMockExecutor)
        {
            all.Add(new ExecutorRegistration { Executor = new MockExecutor() });
        }

        return new ExecutorRegistry(all);
    }
}
