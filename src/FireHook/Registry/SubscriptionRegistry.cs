using System.Collections.Concurrent;
using FireHook.Dispatching;

namespace FireHook.Registry;

public sealed class SubscriptionRegistry
{
    private readonly ConcurrentDictionary<string, List<IWebhookDispatcher>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string eventName, IWebhookDispatcher dispatcher)
    {
        _subscriptions.AddOrUpdate(
            eventName,
            _ => [dispatcher],
            (_, existing) => { lock (existing) { existing.Add(dispatcher); } return existing; });
    }

    public IReadOnlyList<IWebhookDispatcher> GetDispatchers(string eventName)
    {
        if (_subscriptions.TryGetValue(eventName, out var list))
            lock (list) { return list.ToArray(); }

        return [];
    }

    public IEnumerable<string> RegisteredEventNames => _subscriptions.Keys;
}
