using FirebirdSql.Data.FirebirdClient;

namespace FireHook.Tests.Integration;

[CollectionDefinition("Firebird")]
public class FirebirdCollection : ICollectionFixture<FirebirdFixture> { }

public sealed class FirebirdFixture : IAsyncLifetime
{
    private static readonly string DbPath =
        Path.Combine(
            Environment.GetEnvironmentVariable("FIREHOOK_TEST_DB_DIR") ?? Path.GetTempPath(),
            $"firehook_test_{Guid.NewGuid():N}.fdb");

    public string ConnectionString { get; } =
        $"User=SYSDBA;Password=masterkey;Database={DbPath};" +
        "DataSource=localhost;Dialect=3;Charset=UTF8;";

    public async Task InitializeAsync()
    {
        for (int attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await FbConnection.CreateDatabaseAsync(ConnectionString, pageSize: 16384, overwrite: true);
                return;
            }
            catch (FbException) when (attempt < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
        }
    }

    public async Task DisposeAsync()
    {
        FbConnection.ClearAllPools();
        await FbConnection.DropDatabaseAsync(ConnectionString);
    }
}
