using FireHook.Coordination;

namespace FireHook.Tests.Integration;

[Collection("Firebird")]
public class EventCoordinationTableTests(FirebirdFixture db)
{
    [Fact]
    public async Task EnsureSchemaAsync_CreatesTableAndTrigger()
    {
        var table = new EventCoordinationTable(db.ConnectionString);
        // Should not throw; idempotent on repeat calls
        await table.EnsureSchemaAsync();
        await table.EnsureSchemaAsync();
    }

    [Fact]
    public async Task InsertAndFetch_ReturnsInsertedRow()
    {
        var table = new EventCoordinationTable(db.ConnectionString);
        await table.EnsureSchemaAsync();

        await table.InsertEventAsync("order.created", """{"OrderId":1}""");

        var rows = await table.FetchPendingAsync("order.created");
        Assert.Single(rows);
        Assert.Equal("order.created", rows[0].EventName);
        Assert.Equal("""{"OrderId":1}""", rows[0].Payload);
    }

    [Fact]
    public async Task InsertWithNullPayload_Succeeds()
    {
        var table = new EventCoordinationTable(db.ConnectionString);
        await table.EnsureSchemaAsync();

        await table.InsertEventAsync("ping", null);

        var rows = await table.FetchPendingAsync("ping");
        Assert.Contains(rows, r => r.Payload is null);
    }

    [Fact]
    public async Task FetchPendingAsync_ExcludesProcessedRows()
    {
        var table = new EventCoordinationTable(db.ConnectionString);
        await table.EnsureSchemaAsync();

        await table.InsertEventAsync("order.shipped", null);
        var pending = await table.FetchPendingAsync("order.shipped");
        await table.MarkProcessedAsync(pending[0].Id);

        var after = await table.FetchPendingAsync("order.shipped");
        Assert.DoesNotContain(after, r => r.Id == pending[0].Id);
    }

    [Fact]
    public async Task FetchPendingAsync_ExcludesDeadLetteredRows()
    {
        var table = new EventCoordinationTable(db.ConnectionString);
        await table.EnsureSchemaAsync();

        await table.InsertEventAsync("order.failed", null);
        var pending = await table.FetchPendingAsync("order.failed");
        await table.MarkDeadLetterAsync(pending[0].Id);

        var after = await table.FetchPendingAsync("order.failed");
        Assert.DoesNotContain(after, r => r.Id == pending[0].Id);
    }

    [Fact]
    public async Task FetchPendingAsync_OnlyReturnsMatchingEventName()
    {
        var table = new EventCoordinationTable(db.ConnectionString);
        await table.EnsureSchemaAsync();

        await table.InsertEventAsync("event.a", null);
        await table.InsertEventAsync("event.b", null);

        var rows = await table.FetchPendingAsync("event.a");
        Assert.All(rows, r => Assert.Equal("event.a", r.EventName));
    }
}
