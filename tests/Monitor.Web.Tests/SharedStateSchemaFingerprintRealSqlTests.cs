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
            Assert.Equal(2, await CountTrustedCanonicalIntegrityChecksAsync(adminConnectionString));

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

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments DROP CONSTRAINT CK_MonitorSharedStateDocuments_Version;");
            await AssertUnavailableAsync(readiness);
            Assert.Equal(0, await CountDocumentsAsync(runtimeConnectionString));

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments ADD CONSTRAINT CK_MonitorSharedStateDocuments_Version CHECK (Version >= 0);");
            await AssertUnavailableAsync(readiness);

            await ExecuteAsync(
                adminConnectionString,
                """
                ALTER TABLE dbo.MonitorSharedStateDocuments DROP CONSTRAINT CK_MonitorSharedStateDocuments_Version;
                ALTER TABLE dbo.MonitorSharedStateDocuments WITH CHECK ADD CONSTRAINT CK_MonitorSharedStateDocuments_Version CHECK (Version >= 1);
                """);
            await AssertReadyAsync(readiness);

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments NOCHECK CONSTRAINT CK_MonitorSharedStateDocuments_PayloadJson;");
            await AssertUnavailableAsync(readiness);

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments CHECK CONSTRAINT CK_MonitorSharedStateDocuments_PayloadJson;");
            Assert.Equal((false, true), await ReadCheckStateAsync(adminConnectionString, "CK_MonitorSharedStateDocuments_PayloadJson"));
            await AssertUnavailableAsync(readiness);

            await ExecuteAsync(
                adminConnectionString,
                "ALTER TABLE dbo.MonitorSharedStateDocuments WITH CHECK CHECK CONSTRAINT CK_MonitorSharedStateDocuments_PayloadJson;");
            Assert.Equal((false, false), await ReadCheckStateAsync(adminConnectionString, "CK_MonitorSharedStateDocuments_PayloadJson"));
            await AssertReadyAsync(readiness);
            Assert.Equal(0, await CountDocumentsAsync(runtimeConnectionString));
            Assert.Equal(2, await CountTrustedCanonicalIntegrityChecksAsync(adminConnectionString));
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, quotedDatabase);
        }
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task Installer_FreshAndCanonicalRerunSucceed_DriftedExistingTableFailsWithoutStampingV1()
    {
        var required = string.Equals(Environment.GetEnvironmentVariable("MONITOR_REQUIRE_REAL_SQL"), "1", StringComparison.Ordinal);
        if (!required) return;

        var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST");
        var portText = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT");
        var saPassword = Environment.GetEnvironmentVariable("SQL_SA_PASSWORD");
        Assert.False(string.IsNullOrWhiteSpace(host));
        Assert.True(int.TryParse(portText, out var port));
        Assert.False(string.IsNullOrWhiteSpace(saPassword));

        var installerSql = ReadRepositoryFile("scripts", "sql", "monitor_shared_state_v1.sql");
        var masterConnectionString = ConnectionString(host!, port, "sa", saPassword!, "master");
        var canonicalDatabase = $"MonitorStateInstaller_{Guid.NewGuid():N}";
        var driftDatabase = $"MonitorStateInstallerDrift_{Guid.NewGuid():N}";
        var integrityDriftDatabase = $"MonitorStateInstallerChecks_{Guid.NewGuid():N}";
        var quotedCanonicalDatabase = QuoteIdentifier(canonicalDatabase);
        var quotedDriftDatabase = QuoteIdentifier(driftDatabase);
        var quotedIntegrityDriftDatabase = QuoteIdentifier(integrityDriftDatabase);

        await ExecuteAsync(
            masterConnectionString,
            $"CREATE DATABASE {quotedCanonicalDatabase}; CREATE DATABASE {quotedDriftDatabase}; CREATE DATABASE {quotedIntegrityDriftDatabase};");
        try
        {
            var canonicalConnectionString = ConnectionString(host!, port, "sa", saPassword!, canonicalDatabase);
            await ExecuteAsync(canonicalConnectionString, installerSql);
            Assert.Equal(1, await ReadSchemaVersionAsync(canonicalConnectionString));
            Assert.Equal(7, await ReadUpdatedAtScaleAsync(canonicalConnectionString));
            Assert.Equal(2, await CountTrustedCanonicalIntegrityChecksAsync(canonicalConnectionString));

            await ExecuteAsync(canonicalConnectionString, installerSql);
            Assert.Equal(1, await ReadSchemaVersionAsync(canonicalConnectionString));
            Assert.Equal(7, await ReadUpdatedAtScaleAsync(canonicalConnectionString));
            Assert.Equal(2, await CountTrustedCanonicalIntegrityChecksAsync(canonicalConnectionString));

            var driftConnectionString = ConnectionString(host!, port, "sa", saPassword!, driftDatabase);
            await ExecuteAsync(driftConnectionString, """
                CREATE TABLE dbo.MonitorSharedStateDocuments
                (
                    DocumentKey nvarchar(128) NOT NULL CONSTRAINT PK_MonitorSharedStateDocuments PRIMARY KEY,
                    Version bigint NOT NULL,
                    PayloadJson nvarchar(max) NOT NULL,
                    UpdatedAtUtc datetime2(3) NOT NULL
                );
                """);

            var structuralException = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(driftConnectionString, installerSql));
            Assert.Equal(51001, structuralException.Number);
            Assert.Equal(3, await ReadUpdatedAtScaleAsync(driftConnectionString));
            Assert.False(await TableExistsAsync(driftConnectionString, "dbo.MonitorSharedStateSchema"));

            var integrityDriftConnectionString = ConnectionString(host!, port, "sa", saPassword!, integrityDriftDatabase);
            await ExecuteAsync(integrityDriftConnectionString, """
                CREATE TABLE dbo.MonitorSharedStateDocuments
                (
                    DocumentKey nvarchar(128) NOT NULL CONSTRAINT PK_MonitorSharedStateDocuments PRIMARY KEY,
                    Version bigint NOT NULL,
                    PayloadJson nvarchar(max) NOT NULL,
                    UpdatedAtUtc datetime2(7) NOT NULL
                );
                """);

            var integrityException = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(integrityDriftConnectionString, installerSql));
            Assert.Equal(51001, integrityException.Number);
            Assert.Equal(7, await ReadUpdatedAtScaleAsync(integrityDriftConnectionString));
            Assert.Equal(0, await CountTrustedCanonicalIntegrityChecksAsync(integrityDriftConnectionString));
            Assert.False(await TableExistsAsync(integrityDriftConnectionString, "dbo.MonitorSharedStateSchema"));
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, quotedCanonicalDatabase);
            await DropDatabaseAsync(masterConnectionString, quotedDriftDatabase);
            await DropDatabaseAsync(masterConnectionString, quotedIntegrityDriftDatabase);
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
        var value = await ExecuteScalarAsync(connectionString, "SELECT COUNT(*) FROM dbo.MonitorSharedStateDocuments;");
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<int> CountTrustedCanonicalIntegrityChecksAsync(string connectionString)
    {
        var value = await ExecuteScalarAsync(
            connectionString,
            """
            SELECT COUNT(*)
            FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.MonitorSharedStateDocuments')
              AND name IN (N'CK_MonitorSharedStateDocuments_Version', N'CK_MonitorSharedStateDocuments_PayloadJson')
              AND is_disabled = 0
              AND is_not_trusted = 0;
            """);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<(bool Disabled, bool NotTrusted)> ReadCheckStateAsync(string connectionString, string constraintName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT is_disabled, is_not_trusted
            FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.MonitorSharedStateDocuments')
              AND name = @ConstraintName;
            """;
        command.Parameters.AddWithValue("@ConstraintName", constraintName);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetBoolean(0), reader.GetBoolean(1));
    }

    private static async Task<int?> ReadSchemaVersionAsync(string connectionString)
    {
        var value = await ExecuteScalarAsync(
            connectionString,
            "SELECT SchemaVersion FROM dbo.MonitorSharedStateSchema WHERE Id = 1;");
        return value is null or DBNull
            ? null
            : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<int> ReadUpdatedAtScaleAsync(string connectionString)
    {
        var value = await ExecuteScalarAsync(
            connectionString,
            "SELECT scale FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MonitorSharedStateDocuments') AND name = N'UpdatedAtUtc';");
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(@TableName, N'U') IS NULL THEN 0 ELSE 1 END;";
        command.Parameters.AddWithValue("@TableName", tableName);
        var value = await command.ExecuteScalarAsync();
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
        await ExecuteAsync(
            masterConnectionString,
            $"ALTER DATABASE {quotedDatabase} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {quotedDatabase};");
    }

    private static string ReadRepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
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
            ApplicationName = "Monitor.SharedState.SchemaFingerprint.RealSql.Tests"
        }.ConnectionString;

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
}
