using SlackBotBroker.Executors;

namespace SlackBotBroker.Executors.Tests;

public class ExecutorRegistryFactoryTests
{
    [Fact]
    public void MockExecutor_is_absent_when_the_flag_is_unset()
    {
        var registry = ExecutorRegistryFactory.Create([], includeMockExecutor: false);

        Assert.False(registry.TryGet("mock", out _));
    }

    [Fact]
    public void MockExecutor_is_registered_when_the_flag_is_set()
    {
        var registry = ExecutorRegistryFactory.Create([], includeMockExecutor: true);

        Assert.True(registry.TryGet("mock", out var executor));
        Assert.IsType<MockExecutor>(executor);
    }

    [Fact]
    public void Explicit_registrations_are_preserved_alongside_the_flag()
    {
        var other = new MockExecutor("other");
        var registry = ExecutorRegistryFactory.Create([new ExecutorRegistration { Executor = other }], includeMockExecutor: true);

        Assert.True(registry.TryGet("other", out _));
        Assert.True(registry.TryGet("mock", out _));
    }
}
