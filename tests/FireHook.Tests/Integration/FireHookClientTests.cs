using FireHook.Models;
using FireHook.Registry;

namespace FireHook.Tests.Integration;

[Collection("Firebird")]
public class FireHookClientTests(FirebirdFixture db)
{
    [Fact]
    public async Task PublishAsync_ThenSubscribe_HandlerReceivesEvent()
    {
        var registry = new SubscriptionRegistry();
        var client = new FireHookClient(db.ConnectionString, registry);

        var received = new TaskCompletionSource<FireHookEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.Subscribe("integration.test", evt =>
        {
            received.TrySetResult(evt);
            return Task.CompletedTask;
        });

        await client.StartAsync();

        try
        {
            await client.PublishAsync("integration.test", new { Value = 42 });

            var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal("integration.test", evt.EventName);
            Assert.Contains("42", evt.Payload);
        }
        finally
        {
            await client.StopAsync();
        }
    }

    [Fact]
    public async Task PublishAsync_NoPayload_HandlerReceivesNullPayload()
    {
        var registry = new SubscriptionRegistry();
        var client = new FireHookClient(db.ConnectionString, registry);

        var received = new TaskCompletionSource<FireHookEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.Subscribe("no.payload.test", evt =>
        {
            received.TrySetResult(evt);
            return Task.CompletedTask;
        });

        await client.StartAsync();

        try
        {
            await client.PublishAsync("no.payload.test");

            var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Null(evt.Payload);
        }
        finally
        {
            await client.StopAsync();
        }
    }

    [Fact]
    public async Task StartAsync_IsIdempotentForSchema()
    {
        var registry = new SubscriptionRegistry();
        var client = new FireHookClient(db.ConnectionString, registry);

        // Should not throw on repeated schema creation
        await client.EnsureSchemaAsync();
        await client.EnsureSchemaAsync();
    }
}
