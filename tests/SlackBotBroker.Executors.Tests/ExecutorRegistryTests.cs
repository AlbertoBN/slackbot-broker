using SlackBotBroker.Executors;

namespace SlackBotBroker.Executors.Tests;

public class ExecutorRegistryTests
{
    [Fact]
    public void Resolves_a_known_enabled_executor()
    {
        var executor = new MockExecutor("known");
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = executor, Enabled = true }]);

        var resolved = registry.TryGet("known", out var found);

        Assert.True(resolved);
        Assert.Same(executor, found);
    }

    [Fact]
    public void Does_not_resolve_an_unknown_key()
    {
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = new MockExecutor("known") }]);

        var resolved = registry.TryGet("unknown", out var found);

        Assert.False(resolved);
        Assert.Null(found);
    }

    [Fact]
    public void Does_not_resolve_a_disabled_key()
    {
        var registry = new ExecutorRegistry([new ExecutorRegistration { Executor = new MockExecutor("disabled-one"), Enabled = false }]);

        var resolved = registry.TryGet("disabled-one", out var found);

        Assert.False(resolved);
        Assert.Null(found);
    }
}
