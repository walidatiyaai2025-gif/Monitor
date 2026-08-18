using System.Data;
using Microsoft.Data.SqlClient;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedStateAtomicExecutionRealSqlTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task ExecutionLock_BlocksConcurrentSchemaVersionAndDocumentDdlMutation()
    {
        if (!Required()) return;

        var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST")!;
        var port = int.Parse(Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT")!, System.Globalization.CultureInfo.InvariantCulture);
        var runtimeUsername = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_USERNAME")!;
        var runtimePassword = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD")!;
        var saPassword = Environment.GetEnvironmentVariable("SQL_SA_PASSWORD")!;
        var database = $"MonitorStateAtomicExecution_{Guid.NewGuid():N}";
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

            await using var runtimeConnection = new SqlConnection(runtimeConnectionString);
            await runtimeConnection.OpenAsync();
            await using var transaction = (SqlTransaction)await runtimeConnection.BeginTransactionAsync(IsolationLevel.Serializable);

            await SqlServerSharedStateSqlBackend.AcquireExecutionLockAsync(
                runtimeConnection,
                transaction,
                "shared-state:atomic-lock",
                write: false,
                commandTimeoutSeconds: 5,
                CancellationToken.None);

            var versionDrift = await Assert.ThrowsAsync<SqlException>(() => ExecuteWithLockTimeoutAsync(
                adminConnectionString,
                "UPDATE dbo.MonitorSharedStateSchema SET SchemaVersion = 2 WHERE Id = 1;"));
            Assert.Equal(1222, versionDrift.Number);

            var documentDdl = await Assert.ThrowsAsync<SqlException>(() => ExecuteWithLockTimeoutAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments ADD AtomicExecutionDriftProbe int NULL;"));
            Assert.Equal(1222, documentDdl.Number);

            await transaction.RollbackAsync();

            await ExecuteAsync(adminConnectionString, """
                UPDATE dbo.MonitorSharedStateSchema SET SchemaVersion = 2 WHERE Id = 1;
                UPDATE dbo.MonitorSharedStateSchema SET SchemaVersion = 1 WHERE Id = 1;
                ALTER TABLE dbo.MonitorSharedStateDocuments ADD AtomicExecutionDriftProbe int NULL;
                ALTER TABLE dbo.MonitorSharedStateDocuments DROP COLUMN AtomicExecutionDriftProbe;
                """);
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

    private static async Task ExecuteWithLockTimeoutAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SET LOCK_TIMEOUT 500; {sql}";
        command.CommandTimeout = 5;
        await command.ExecuteNonQueryAsync();
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
            ApplicationName = "Monitor.SharedState.AtomicExecution.RealSql.Tests"
        }.ConnectionString;

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
}
