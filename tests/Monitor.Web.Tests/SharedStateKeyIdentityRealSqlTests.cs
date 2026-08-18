using Microsoft.Data.SqlClient;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedStateKeyIdentityRealSqlTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task CaseInsensitiveDatabase_DoesNotAliasOrMutateDifferentCaseDocumentKey()
    {
        if (!Required()) return;

        var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST")!;
        var port = int.Parse(Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT")!, System.Globalization.CultureInfo.InvariantCulture);
        var runtimeUsername = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_USERNAME")!;
        var runtimePassword = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD")!;
        var saPassword = Environment.GetEnvironmentVariable("SQL_SA_PASSWORD")!;
        var database = $"MonitorStateKeyIdentity_{Guid.NewGuid():N}";
        var quotedDatabase = QuoteIdentifier(database);
        var quotedRuntimeLogin = QuoteIdentifier(runtimeUsername);
        var masterConnectionString = ConnectionString(host, port, "sa", saPassword, "master");
        var adminConnectionString = ConnectionString(host, port, "sa", saPassword, database);
        var runtimeConnectionString = ConnectionString(host, port, runtimeUsername, runtimePassword, database);

        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE {quotedDatabase} COLLATE Latin1_General_100_CI_AS;");
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

            const string persistedKey = "monitor:lease:v1:CaseProbe";
            const string aliasKey = "monitor:lease:v1:caseprobe";
            const string originalPayload = "{\"value\":\"original\"}";
            const string aliasPayload = "{\"value\":\"alias-write\"}";
            const string exactPayload = "{\"value\":\"exact-write\"}";

            await ExecuteAsync(
                adminConnectionString,
                $"INSERT dbo.MonitorSharedStateDocuments (DocumentKey, Version, PayloadJson, UpdatedAtUtc) VALUES (N'{persistedKey}', 1, N'{originalPayload.Replace("'", "''", StringComparison.Ordinal)}', SYSUTCDATETIME());");

            var store = new SqlServerSharedStateDocumentStore(
                new SharedStateOptions
                {
                    Provider = SharedStateProviderKind.SqlServer,
                    ConnectionStringEnvironmentVariable = "MONITOR_TEST_SHARED_STATE_KEY_IDENTITY",
                    CommandTimeoutSeconds = 5
                },
                new SqlServerSharedStateSqlBackend(),
                _ => runtimeConnectionString);

            var exact = await store.ReadAsync(persistedKey);
            Assert.NotNull(exact);
            Assert.Equal(persistedKey, exact.Key);
            Assert.Equal(1, exact.Version);
            Assert.Equal(originalPayload, exact.PayloadJson);

            await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(() => store.ReadAsync(aliasKey));

            await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(
                () => store.CompareExchangeAsync(aliasKey, 1, aliasPayload));

            var afterAlias = await ReadPersistedDocumentAsync(adminConnectionString, persistedKey);
            Assert.Equal(1, afterAlias.Version);
            Assert.Equal(originalPayload, afterAlias.PayloadJson);

            var exactWrite = await store.CompareExchangeAsync(persistedKey, 1, exactPayload);
            Assert.True(exactWrite.Applied);
            Assert.NotNull(exactWrite.Document);
            Assert.Equal(persistedKey, exactWrite.Document.Key);
            Assert.Equal(2, exactWrite.Document.Version);
            Assert.Equal(exactPayload, exactWrite.Document.PayloadJson);
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

    private static async Task<(long Version, string PayloadJson)> ReadPersistedDocumentAsync(string connectionString, string key)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Version, PayloadJson FROM dbo.MonitorSharedStateDocuments WHERE DocumentKey = @DocumentKey;";
        command.Parameters.AddWithValue("@DocumentKey", key);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetString(1));
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
            ApplicationName = "Monitor.SharedState.KeyIdentity.RealSql.Tests"
        }.ConnectionString;

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
}
