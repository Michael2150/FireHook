using FireHook.Dispatching;
using FireHook.Models;
using FireHook.Registry;

namespace FireHook.Tests.Unit;

public class SubscriptionRegistryTests
{
    [Fact]
    public void GetDispatchers_UnknownEvent_ReturnsEmpty()
    {
        var registry = new SubscriptionRegistry();
        Assert.Empty(registry.GetDispatchers("does.not.exist"));
    }

    [Fact]
    public void Register_SingleDispatcher_CanBeRetrieved()
    {
        var registry = new SubscriptionRegistry();
        var dispatcher = new SpyDispatcher();

        registry.Register("order.created", dispatcher);

        Assert.Single(registry.GetDispatchers("order.created"));
    }

    [Fact]
    public void Register_MultipleDispatchers_AllReturned()
    {
        var registry = new SubscriptionRegistry();
        registry.Register("order.created", new SpyDispatcher());
        registry.Register("order.created", new SpyDispatcher());
        registry.Register("order.created", new SpyDispatcher());

        Assert.Equal(3, registry.GetDispatchers("order.created").Count);
    }

    [Fact]
    public void Register_DifferentEvents_IsolatedFromEachOther()
    {
        var registry = new SubscriptionRegistry();
        registry.Register("event.a", new SpyDispatcher());
        registry.Register("event.b", new SpyDispatcher());

        Assert.Single(registry.GetDispatchers("event.a"));
        Assert.Single(registry.GetDispatchers("event.b"));
    }

    [Fact]
    public void GetDispatchers_IsCaseInsensitive()
    {
        var registry = new SubscriptionRegistry();
        registry.Register("Order.Created", new SpyDispatcher());

        Assert.Single(registry.GetDispatchers("order.created"));
        Assert.Single(registry.GetDispatchers("ORDER.CREATED"));
    }

    [Fact]
    public void RegisteredEventNames_ReflectsAllRegisteredEvents()
    {
        var registry = new SubscriptionRegistry();
        registry.Register("event.a", new SpyDispatcher());
        registry.Register("event.b", new SpyDispatcher());

        Assert.Contains("event.a", registry.RegisteredEventNames);
        Assert.Contains("event.b", registry.RegisteredEventNames);
    }

    private sealed class SpyDispatcher : IWebhookDispatcher
    {
        public Task DispatchAsync(FireHookEvent evt, CancellationToken ct = default) => Task.CompletedTask;
    }
}
