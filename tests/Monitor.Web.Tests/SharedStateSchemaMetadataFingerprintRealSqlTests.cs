using Microsoft.Data.SqlClient;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedStateSchemaMetadataFingerprintRealSqlTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task Readiness_RequiresCanonicalSchemaMetadataFingerprint()
    {
        if (!Required()) return;

        var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST")!;
        var port = int.Parse(Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT")!, System.Globalization.CultureInfo.InvariantCulture);
        var runtimeUsername = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_USERNAME")!;
        var runtimePassword = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD")!;
        var saPassword = Environment.GetEnvironmentVariable("SQL_SA_PASSWORD")!;
        var database = $"MonitorStateSchemaMetadata_{Guid.NewGuid():N}";
        var quotedDatabase = QuoteIdentifier(database);
        var quotedRuntimeLogin = QuoteIdentifier(runtimeUsername);
        var master = ConnectionString(host, port, "sa", saPassword, "master");
        var admin = ConnectionString(host, port, "sa", saPassword, database);
        var runtime = ConnectionString(host, port, runtimeUsername, runtimePassword, database);

        await ExecuteAsync(master, $"CREATE DATABASE {quotedDatabase};");
        try
        {
            await ExecuteAsync(admin, ReadRepositoryFile("scripts", "sql", "monitor_shared_state_v1.sql"));
            await ExecuteAsync(admin, """
                CREATE ROLE MonitorStateRuntime AUTHORIZATION dbo;
                GRANT SELECT ON dbo.MonitorSharedStateSchema TO MonitorStateRuntime;
                GRANT SELECT, INSERT, UPDATE ON dbo.MonitorSharedStateDocuments TO MonitorStateRuntime;
                """);
            await ExecuteAsync(admin, $"CREATE USER {quotedRuntimeLogin} FOR LOGIN {quotedRuntimeLogin}; ALTER ROLE MonitorStateRuntime ADD MEMBER {quotedRuntimeLogin};");

            var options = new SharedStateOptions
            {
                Provider = SharedStateProviderKind.SqlServer,
                ConnectionStringEnvironmentVariable = "MONITOR_SHARED_STATE_SQL_CONNECTION",
                CommandTimeoutSeconds = 5
            };
            var store = new SqlServerSharedStateDocumentStore(options, new SqlServerSharedStateSqlBackend(), _ => runtime);
            var readiness = new SharedStateReadinessService(options, store);

            await AssertReadyAsync(readiness);
            Assert.Equal(0, await CountDocumentsAsync(runtime));

            await ExecuteAsync(admin, "ALTER TABLE dbo.MonitorSharedStateSchema DROP CONSTRAINT PK_MonitorSharedStateSchema;");
            await AssertUnavailableAsync(readiness);
            await ExecuteAsync(admin, "ALTER TABLE dbo.MonitorSharedStateSchema ADD CONSTRAINT PK_MonitorSharedStateSchema PRIMARY KEY (Id);");
            await AssertReadyAsync(readiness);

            await ExecuteAsync(admin, "ALTER TABLE dbo.MonitorSharedStateSchema NOCHECK CONSTRAINT CK_MonitorSharedStateSchema_Id;");
            await AssertUnavailableAsync(readiness);
            await ExecuteAsync(admin, "ALTER TABLE dbo.MonitorSharedStateSchema CHECK CONSTRAINT CK_MonitorSharedStateSchema_Id;");
            Assert.Equal((false, true), await ReadCheckStateAsync(admin));
            await AssertUnavailableAsync(readiness);
            await ExecuteAsync(admin, "ALTER TABLE dbo.MonitorSharedStateSchema WITH CHECK CHECK CONSTRAINT CK_MonitorSharedStateSchema_Id;");
            Assert.Equal((false, false), await ReadCheckStateAsync(admin));
            await AssertReadyAsync(readiness);

            await ExecuteAsync(admin, "ALTER TABLE dbo.MonitorSharedStateSchema DROP CONSTRAINT DF_MonitorSharedStateSchema_InstalledAtUtc;");
            await AssertUnavailableAsync(readiness);
            await ExecuteAsync(admin, "ALTER TABLE dbo.MonitorSharedStateSchema ADD CONSTRAINT DF_MonitorSharedStateSchema_InstalledAtUtc DEFAULT GETUTCDATE() FOR InstalledAtUtc;");
            await AssertUnavailableAsync(readiness);
            await ExecuteAsync(admin, """
                ALTER TABLE dbo.MonitorSharedStateSchema DROP CONSTRAINT DF_MonitorSharedStateSchema_InstalledAtUtc;
                ALTER TABLE dbo.MonitorSharedStateSchema ADD CONSTRAINT DF_MonitorSharedStateSchema_InstalledAtUtc DEFAULT SYSUTCDATETIME() FOR InstalledAtUtc;
                """);
            await AssertReadyAsync(readiness);

            await ExecuteAsync(admin, "ALTER TABLE dbo.MonitorSharedStateSchema ADD DriftMarker bit NULL;");
            await AssertUnavailableAsync(readiness);
            Assert.Equal(0, await CountDocumentsAsync(runtime));
            await ExecuteAsync(admin, "ALTER TABLE dbo.MonitorSharedStateSchema DROP COLUMN DriftMarker;");
            await AssertReadyAsync(readiness);
            Assert.Equal(0, await CountDocumentsAsync(runtime));
        }
        finally
        {
            await DropDatabaseAsync(master, quotedDatabase);
        }
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task Installer_RejectsPreExistingMetadataDriftBeforeDocumentProvisioning()
    {
        if (!Required()) return;

        var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST")!;
        var port = int.Parse(Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT")!, System.Globalization.CultureInfo.InvariantCulture);
        var saPassword = Environment.GetEnvironmentVariable("SQL_SA_PASSWORD")!;
        var database = $"MonitorStateSchemaMetadataInstaller_{Guid.NewGuid():N}";
        var quotedDatabase = QuoteIdentifier(database);
        var master = ConnectionString(host, port, "sa", saPassword, "master");
        var admin = ConnectionString(host, port, "sa", saPassword, database);

        await ExecuteAsync(master, $"CREATE DATABASE {quotedDatabase};");
        try
        {
            await ExecuteAsync(admin, """
                CREATE TABLE dbo.MonitorSharedStateSchema
                (
                    Id tinyint NOT NULL,
                    SchemaVersion int NOT NULL,
                    InstalledAtUtc datetime2(7) NOT NULL
                );
                INSERT dbo.MonitorSharedStateSchema (Id, SchemaVersion, InstalledAtUtc)
                VALUES (1, 1, SYSUTCDATETIME());
                """);

            var exception = await Assert.ThrowsAsync<SqlException>(() =>
                ExecuteAsync(admin, ReadRepositoryFile("scripts", "sql", "monitor_shared_state_v1.sql")));

            Assert.Equal(51002, exception.Number);
            Assert.False(await TableExistsAsync(admin, "dbo.MonitorSharedStateDocuments"));
            Assert.Equal(1, await CountRowsAsync(admin, "dbo.MonitorSharedStateSchema"));
            Assert.Equal(0, await CountMetadataContractObjectsAsync(admin));
        }
        finally
        {
            await DropDatabaseAsync(master, quotedDatabase);
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

    private static async Task AssertUnavailableAsync(SharedStateReadinessService readiness)
    {
        var result = await readiness.GetAsync();
        Assert.Equal(SharedStateReadinessStatus.Unavailable, result.Status);
        Assert.False(result.SharedStorageReady);
        Assert.Null(result.SchemaVersion);
    }

    private static async Task<(bool Disabled, bool NotTrusted)> ReadCheckStateAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT is_disabled, is_not_trusted
            FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.MonitorSharedStateSchema')
              AND name = N'CK_MonitorSharedStateSchema_Id';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetBoolean(0), reader.GetBoolean(1));
    }

    private static async Task<int> CountDocumentsAsync(string connectionString) =>
        Convert.ToInt32(await ExecuteScalarAsync(connectionString, "SELECT COUNT(*) FROM dbo.MonitorSharedStateDocuments;"), System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<int> CountRowsAsync(string connectionString, string tableName) =>
        Convert.ToInt32(await ExecuteScalarAsync(connectionString, $"SELECT COUNT(*) FROM {tableName};"), System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<int> CountMetadataContractObjectsAsync(string connectionString)
    {
        var value = await ExecuteScalarAsync(connectionString, """
            SELECT
                (SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MonitorSharedStateSchema') AND is_primary_key = 1)
              + (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MonitorSharedStateSchema'))
              + (SELECT COUNT(*) FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MonitorSharedStateSchema'));
            """);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        var value = await ExecuteScalarAsync(connectionString, $"SELECT CASE WHEN OBJECT_ID(N'{tableName}', N'U') IS NULL THEN 0 ELSE 1 END;");
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) == 1;
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
        await ExecuteAsync(masterConnectionString, $"ALTER DATABASE {quotedDatabase} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {quotedDatabase};");
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
            ApplicationName = "Monitor.SharedState.SchemaMetadata.RealSql.Tests"
        }.ConnectionString;

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
}
