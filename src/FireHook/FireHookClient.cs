using System.Text.Json;
using FireHook.Coordination;
using FireHook.Dispatching;
using FireHook.Listening;
using FireHook.Models;
using FireHook.Registry;

namespace FireHook;

public sealed class FireHookClient : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SubscriptionRegistry _registry;
    private readonly EventCoordinationTable _coordination;
    private FbEventListener? _listener;

    public FireHookClient(string connectionString, SubscriptionRegistry registry)
    {
        _connectionString = connectionString;
        _registry = registry;
        _coordination = new EventCoordinationTable(connectionString);
    }

    public void Subscribe(string eventName, Func<FireHookEvent, Task> handler) =>
        _registry.Register(eventName, new InProcessDispatcher(handler));

    public void Subscribe(string eventName, IWebhookDispatcher dispatcher) =>
        _registry.Register(eventName, dispatcher);

    /// <summary>
    /// Publishes an event by inserting a row into FIREHOOK_EVENTS. The database trigger
    /// fires POST_EVENT automatically, notifying all connected clients.
    /// Requires the schema to be initialized first (via StartAsync or EnsureSchemaAsync).
    /// </summary>
    public async Task PublishAsync(string eventName, object? payload = null, CancellationToken ct = default)
    {
        var json = payload is null ? null : JsonSerializer.Serialize(payload);
        await _coordination.InsertEventAsync(eventName, json, ct);
    }

    public async Task PublishAsync<T>(string eventName, T payload, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload);
        await _coordination.InsertEventAsync(eventName, json, ct);
    }

    /// <summary>
    /// Fires POST_EVENT directly without storing a row. Useful for lightweight signals
    /// where no payload is needed and at-least-once delivery is not required.
    /// </summary>
    public Task PingAsync(string eventName, CancellationToken ct = default) =>
        _coordination.FireDirectAsync(eventName, ct);

    /// <summary>
    /// Ensures the FIREHOOK_EVENTS table and trigger exist. Called automatically by
    /// StartAsync; call this directly when using PublishAsync without subscribing.
    /// </summary>
    public Task EnsureSchemaAsync(CancellationToken ct = default) =>
        _coordination.EnsureSchemaAsync(ct);

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _coordination.EnsureSchemaAsync(ct);

        var eventNames = _registry.RegisteredEventNames.ToArray();
        if (eventNames.Length == 0)
            return;

        _listener = new FbEventListener(_connectionString, eventNames, OnFirebirdEventAsync);
        await _listener.StartAsync(ct);
    }

    public async Task StopAsync()
    {
        if (_listener is not null)
            await _listener.StopAsync();
    }

    private async Task OnFirebirdEventAsync(string eventName)
    {
        var dispatchers = _registry.GetDispatchers(eventName);
        if (dispatchers.Count == 0)
            return;

        var events = await _coordination.FetchPendingAsync(eventName);

        foreach (var evt in events)
        {
            var anySucceeded = await DispatchToAllAsync(evt, dispatchers);

            if (anySucceeded)
                await _coordination.MarkProcessedAsync(evt.Id);
            else
                await _coordination.MarkDeadLetterAsync(evt.Id);
        }
    }

    // Returns true if at least one dispatcher succeeded.
    private static async Task<bool> DispatchToAllAsync(FireHookEvent evt, IReadOnlyList<IWebhookDispatcher> dispatchers)
    {
        var anySucceeded = false;

        foreach (var dispatcher in dispatchers)
        {
            try
            {
                await dispatcher.DispatchAsync(evt);
                anySucceeded = true;
            }
            catch { /* failure logged via dead-letter; other dispatchers still run */ }
        }

        return anySucceeded;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        if (_listener is not null)
            await _listener.DisposeAsync();
    }
}
