using Microsoft.Data.SqlClient;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedStateExecutionPreflightRealSqlTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task Store_SchemaVersionDriftBlocksReadAndCasUntilCanonicalVersionIsRestored()
    {
        if (!Required()) return;

        var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST")!;
        var port = int.Parse(Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT")!, System.Globalization.CultureInfo.InvariantCulture);
        var runtimeUsername = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_USERNAME")!;
        var runtimePassword = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD")!;
        var saPassword = Environment.GetEnvironmentVariable("SQL_SA_PASSWORD")!;
        var database = $"MonitorStateExecutionPreflight_{Guid.NewGuid():N}";
        var quotedDatabase = QuoteIdentifier(database);
        var quotedRuntimeLogin = QuoteIdentifier(runtimeUsername);
        var masterConnectionString = ConnectionString(host, port, "sa", saPassword, "master");
        var adminConnectionString = ConnectionString(host, port, "sa", saPassword, database);
        var runtimeConnectionString = ConnectionString(host, port, runtimeUsername, runtimePassword, database);

        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE {quotedDatabase};");
        try
        {
            await ExecuteAsync(adminConnectionString, ReadRepositoryFile("scripts", "sql", "monitor_shared_state_v1.sql"));
            await ExecuteAsync(adminConnectionString, """
                CREATE ROLE MonitorStateRuntime AUTHORIZATION dbo;
                GRANT SELECT ON dbo.MonitorSharedStateSchema TO MonitorStateRuntime;
                GRANT SELECT, INSERT, UPDATE ON dbo.MonitorSharedStateDocuments TO MonitorStateRuntime;
                """);
            await ExecuteAsync(
                adminConnectionString,
                $"CREATE USER {quotedRuntimeLogin} FOR LOGIN {quotedRuntimeLogin}; ALTER ROLE MonitorStateRuntime ADD MEMBER {quotedRuntimeLogin};");

            var options = new SharedStateOptions
            {
                Provider = SharedStateProviderKind.SqlServer,
                ConnectionStringEnvironmentVariable = "MONITOR_SHARED_STATE_SQL_CONNECTION",
                CommandTimeoutSeconds = 5
            };
            var store = new SqlServerSharedStateDocumentStore(
                options,
                new SqlServerSharedStateSqlBackend(),
                _ => runtimeConnectionString);
            var readiness = new SharedStateReadinessService(options, store);
            const string key = "shared-state:execution-preflight";

            var created = await store.CompareExchangeAsync(key, 0, "{\"value\":1}");
            Assert.True(created.Applied);
            Assert.Equal(1, created.Document?.Version);
            Assert.Equal("{\"value\":1}", created.Document?.PayloadJson);
            await AssertReadyAsync(readiness);

            var baseline = await ReadDocumentRowAsync(adminConnectionString, key);
            Assert.Equal((1L, "{\"value\":1}"), baseline);

            await ExecuteAsync(
                adminConnectionString,
                "UPDATE dbo.MonitorSharedStateSchema SET SchemaVersion = 2 WHERE Id = 1;");

            var mismatch = await readiness.GetAsync();
            Assert.Equal(SharedStateReadinessStatus.SchemaMismatch, mismatch.Status);
            Assert.False(mismatch.SharedStorageReady);
            Assert.Equal(2, mismatch.SchemaVersion);

            var readException = await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(() =>
                store.ReadAsync(key));
            var writeException = await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(() =>
                store.CompareExchangeAsync(key, 1, "{\"value\":2}"));

            Assert.Equal("Shared state provider is unavailable.", readException.Message);
            Assert.Equal("Shared state provider is unavailable.", writeException.Message);
            Assert.Equal(baseline, await ReadDocumentRowAsync(adminConnectionString, key));
            Assert.Equal(1, await CountDocumentsAsync(adminConnectionString));

            await ExecuteAsync(
                adminConnectionString,
                "UPDATE dbo.MonitorSharedStateSchema SET SchemaVersion = 1 WHERE Id = 1;");

            await AssertReadyAsync(readiness);
            var observed = await store.ReadAsync(key);
            Assert.Equal(1, observed?.Version);
            Assert.Equal("{\"value\":1}", observed?.PayloadJson);

            var updated = await store.CompareExchangeAsync(key, 1, "{\"value\":2}");
            Assert.True(updated.Applied);
            Assert.Equal(2, updated.Document?.Version);
            Assert.Equal("{\"value\":2}", updated.Document?.PayloadJson);
            Assert.Equal((2L, "{\"value\":2}"), await ReadDocumentRowAsync(adminConnectionString, key));
            Assert.Equal(1, await CountDocumentsAsync(adminConnectionString));
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, quotedDatabase);
        }
    }

    private static bool Required()
    {
        var required = string.Equals(Environment.GetEnvironmentVariable("MONITOR_REQUIRE_REAL_SQL"), "1", StringComparison.Ordinal);
        if (!required) return false;
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST")));
        Assert.True(int.TryParse(Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT"), out _));
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_USERNAME")));
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD")));
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQL_SA_PASSWORD")));
        return true;
    }

    private static async Task AssertReadyAsync(SharedStateReadinessService readiness)
    {
        var result = await readiness.GetAsync();
        Assert.Equal(SharedStateReadinessStatus.Ready, result.Status);
        Assert.True(result.SharedStorageReady);
        Assert.Equal(SqlServerSharedStateDocumentStore.SupportedSchemaVersion, result.SchemaVersion);
    }

    private static async Task<(long Version, string PayloadJson)> ReadDocumentRowAsync(string connectionString, string key)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Version, PayloadJson
            FROM dbo.MonitorSharedStateDocuments
            WHERE DocumentKey = @DocumentKey;
            """;
        command.Parameters.AddWithValue("@DocumentKey", key);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetString(1));
    }

    private static async Task<int> CountDocumentsAsync(string connectionString)
    {
        var value = await ExecuteScalarAsync(connectionString, "SELECT COUNT(*) FROM dbo.MonitorSharedStateDocuments;");
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<object?> ExecuteScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        return await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string masterConnectionString, string quotedDatabase)
    {
        await ExecuteAsync(
            masterConnectionString,
            $"ALTER DATABASE {quotedDatabase} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {quotedDatabase};");
    }

    private static string ReadRepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException($"Repository file was not found: {string.Join('/', pathSegments)}");
    }

    private static string ConnectionString(string host, int port, string username, string password, string database) =>
        new SqlConnectionStringBuilder
        {
            DataSource = $"{host},{port}",
            InitialCatalog = database,
            UserID = username,
            Password = password,
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 5,
            Pooling = false,
            ApplicationName = "Monitor.SharedState.ExecutionPreflight.RealSql.Tests"
        }.ConnectionString;

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
}
