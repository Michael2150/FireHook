using FireHook.Dispatching;
using FireHook.Models;

namespace FireHook.Tests.Unit;

public class InProcessDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_CallsHandler()
    {
        FireHookEvent? received = null;
        var dispatcher = new InProcessDispatcher(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var expected = new FireHookEvent { Id = 1, EventName = "test", Payload = "{}", FiredAt = DateTime.UtcNow };
        await dispatcher.DispatchAsync(expected);

        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task DispatchAsync_PropagatesHandlerException()
    {
        var dispatcher = new InProcessDispatcher(_ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(new FireHookEvent { EventName = "test" }));
    }
}
