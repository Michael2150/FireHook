using FireHook.Models;

namespace FireHook.Dispatching;

public sealed class InProcessDispatcher(Func<FireHookEvent, Task> handler) : IWebhookDispatcher
{
    public Task DispatchAsync(FireHookEvent evt, CancellationToken ct = default) => handler(evt);
}
