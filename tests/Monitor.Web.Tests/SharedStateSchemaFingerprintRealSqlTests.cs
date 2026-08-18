using Microsoft.Data.SqlClient;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedStateSchemaFingerprintRealSqlTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task Readiness_SupportedVersionRequiresCanonicalDocumentTableFingerprint()
    {
        var required = string.Equals(Environment.GetEnvironmentVariable("MONITOR_REQUIRE_REAL_SQL"), "1", StringComparison.Ordinal);
        if (!required) return;

        var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST");
        var portText = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT");
        var runtimeUsername = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_USERNAME");
        var runtimePassword = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD");
        var saPassword = Environment.GetEnvironmentVariable("SQL_SA_PASSWORD");
        Assert.False(string.IsNullOrWhiteSpace(host));
        Assert.True(int.TryParse(portText, out var port));
        Assert.False(string.IsNullOrWhiteSpace(runtimeUsername));
        Assert.False(string.IsNullOrWhiteSpace(runtimePassword));
        Assert.False(string.IsNullOrWhiteSpace(saPassword));

        var database = $"MonitorStateFingerprint_{Guid.NewGuid():N}";
        var masterConnectionString = ConnectionString(host!, port, "sa", saPassword!, "master");
        var adminConnectionString = ConnectionString(host!, port, "sa", saPassword!, database);
        var runtimeConnectionString = ConnectionString(host!, port, runtimeUsername!, runtimePassword!, database);
        var quotedDatabase = QuoteIdentifier(database);
        var quotedRuntimeLogin = QuoteIdentifier(runtimeUsername!);

        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE {quotedDatabase};");
        try
        {
            await ExecuteAsync(adminConnectionString, """
                SET NOCOUNT ON;
                CREATE TABLE dbo.MonitorSharedStateSchema
                (
                    Id tinyint NOT NULL PRIMARY KEY,
                    SchemaVersion int NOT NULL,
                    InstalledAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT CK_MonitorSharedStateSchema_Id CHECK (Id = 1)
                );
                INSERT dbo.MonitorSharedStateSchema (Id, SchemaVersion) VALUES (1, 1);

                CREATE TABLE dbo.MonitorSharedStateDocuments
                (
                    DocumentKey nvarchar(128) NOT NULL,
                    Version bigint NOT NULL,
                    PayloadJson nvarchar(max) NOT NULL,
                    UpdatedAtUtc datetime2(7) NOT NULL,
                    CONSTRAINT PK_MonitorSharedStateDocuments PRIMARY KEY (DocumentKey),
                    CONSTRAINT CK_MonitorSharedStateDocuments_Version CHECK (Version >= 1),
                    CONSTRAINT CK_MonitorSharedStateDocuments_PayloadJson CHECK (ISJSON(PayloadJson) = 1)
                );

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

            await AssertReadyAsync(readiness);
            Assert.Equal(0, await CountDocumentsAsync(runtimeConnectionString));

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments ALTER COLUMN UpdatedAtUtc datetime2(3) NOT NULL;");
            await AssertUnavailableAsync(readiness);
            Assert.Equal(0, await CountDocumentsAsync(runtimeConnectionString));

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments ALTER COLUMN UpdatedAtUtc datetime2(7) NOT NULL;");
            await AssertReadyAsync(readiness);

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments DROP CONSTRAINT PK_MonitorSharedStateDocuments;");
            await AssertUnavailableAsync(readiness);
            Assert.Equal(0, await CountDocumentsAsync(runtimeConnectionString));

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments ADD CONSTRAINT PK_MonitorSharedStateDocuments PRIMARY KEY (DocumentKey);");
            await AssertReadyAsync(readiness);

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments ADD DriftMarker bit NULL;");
            await AssertUnavailableAsync(readiness);
            Assert.Equal(0, await CountDocumentsAsync(runtimeConnectionString));

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments DROP COLUMN DriftMarker;");
            await AssertReadyAsync(readiness);
            Assert.Equal(0, await CountDocumentsAsync(runtimeConnectionString));
        }
        finally
        {
            await ExecuteAsync(
                masterConnectionString,
                $"ALTER DATABASE {quotedDatabase} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {quotedDatabase};");
        }
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

    private static async Task<int> CountDocumentsAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.MonitorSharedStateDocuments;";
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
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
            ApplicationName = "Monitor.SharedState.SchemaFingerprint.RealSql.Tests"
        }.ConnectionString;

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
}
