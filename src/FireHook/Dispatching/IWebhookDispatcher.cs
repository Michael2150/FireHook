using FireHook.Models;

namespace FireHook.Dispatching;

public interface IWebhookDispatcher
{
    Task DispatchAsync(FireHookEvent evt, CancellationToken ct = default);
}
