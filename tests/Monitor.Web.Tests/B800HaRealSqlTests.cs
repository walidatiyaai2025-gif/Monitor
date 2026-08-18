using System.Data;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800HaRealSqlTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task HaEvidence_QueryIsSafeOnSql2022AcceptanceTarget()
    {
        var required = string.Equals(Environment.GetEnvironmentVariable("MONITOR_REQUIRE_REAL_SQL"), "1", StringComparison.Ordinal);
        if (!required) return;

        var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST");
        var portText = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT");
        var username = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_USERNAME");
        var password = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD");
        Assert.False(string.IsNullOrWhiteSpace(host));
        Assert.True(int.TryParse(portText, out var port));
        Assert.False(string.IsNullOrWhiteSpace(username));
        Assert.False(string.IsNullOrWhiteSpace(password));

        var registration = new ServerRegistration(
            Guid.Parse("67676767-6767-6767-6767-676767676767"),
            "B800 HA SQL 2022",
            new SqlServerEndpoint(host!, port, encrypt: true, trustServerCertificate: true),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference("b800-ha-real-sql"),
            true,
            DateTimeOffset.UtcNow);

        var row = await new HaSnapshotQuery().ExecuteAsync(
            registration,
            new SqlLoginSecret(username!, password!),
            CancellationToken.None);
        var evidence = HaEvidenceMapper.Map(row);

        Assert.True(evidence.TotalReplicas >= 0);
        Assert.True(evidence.TotalDatabaseReplicas >= 0);
        Assert.Equal(Math.Min(evidence.TotalReplicas, HaSnapshotQuery.MaxReplicas), evidence.Replicas?.Count ?? 0);
        Assert.Equal(Math.Min(evidence.TotalDatabaseReplicas, HaSnapshotQuery.MaxDatabaseReplicas), evidence.DatabaseReplicas?.Count ?? 0);
        Assert.Equal(evidence.TotalReplicas > HaSnapshotQuery.MaxReplicas, evidence.ReplicasTruncated);
        Assert.Equal(evidence.TotalDatabaseReplicas > HaSnapshotQuery.MaxDatabaseReplicas, evidence.DatabaseReplicasTruncated);

        if (!evidence.IsHadrEnabled)
        {
            Assert.Empty(evidence.Replicas ?? []);
            Assert.Empty(evidence.DatabaseReplicas ?? []);
            return;
        }

        Assert.All(evidence.Replicas ?? [], replica =>
        {
            Assert.False(string.IsNullOrWhiteSpace(replica.GroupKey));
            Assert.False(string.IsNullOrWhiteSpace(replica.ReplicaKey));
            Assert.False(string.IsNullOrWhiteSpace(replica.AvailabilityMode));
            Assert.False(string.IsNullOrWhiteSpace(replica.FailoverMode));
        });
        Assert.All(evidence.DatabaseReplicas ?? [], database =>
        {
            Assert.False(string.IsNullOrWhiteSpace(database.GroupKey));
            Assert.False(string.IsNullOrWhiteSpace(database.ReplicaKey));
            Assert.False(string.IsNullOrWhiteSpace(database.DatabaseKey));
            if (database.LogSendQueueKb.HasValue) Assert.True(database.LogSendQueueKb.Value >= 0);
            if (database.RedoQueueKb.HasValue) Assert.True(database.RedoQueueKb.Value >= 0);
            if (database.SecondaryLagSeconds.HasValue) Assert.True(database.SecondaryLagSeconds.Value >= 0);
        });
    }

    [Fact]
    [Trait("Category", "RealSql")]
    public async Task SharedStateBackend_OversizedRuntimeRowsAreRejectedOnReadAndConflict()
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

        var database = $"MonitorStateAcceptance_{Guid.NewGuid():N}";
        var masterConnectionString = ConnectionString(host!, port, "sa", saPassword!, "master");
        var stateAdminConnectionString = ConnectionString(host!, port, "sa", saPassword!, database);
        var runtimeConnectionString = ConnectionString(host!, port, runtimeUsername!, runtimePassword!, database);
        var quotedDatabase = QuoteIdentifier(database);
        var quotedRuntimeLogin = QuoteIdentifier(runtimeUsername!);

        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE {quotedDatabase};");
        try
        {
            await ExecuteAsync(stateAdminConnectionString, """
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
                    DocumentKey nvarchar(128) NOT NULL PRIMARY KEY,
                    Version bigint NOT NULL,
                    PayloadJson nvarchar(max) NOT NULL,
                    UpdatedAtUtc datetime2(7) NOT NULL,
                    CONSTRAINT CK_MonitorSharedStateDocuments_Version CHECK (Version >= 1),
                    CONSTRAINT CK_MonitorSharedStateDocuments_PayloadJson CHECK (ISJSON(PayloadJson) = 1)
                );

                CREATE ROLE MonitorStateRuntime AUTHORIZATION dbo;
                GRANT SELECT ON dbo.MonitorSharedStateSchema TO MonitorStateRuntime;
                GRANT SELECT, INSERT, UPDATE ON dbo.MonitorSharedStateDocuments TO MonitorStateRuntime;
                """);
            await ExecuteAsync(
                stateAdminConnectionString,
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
            var readinessService = new SharedStateReadinessService(options, store);

            var ready = await readinessService.GetAsync();
            Assert.Equal(SharedStateReadinessStatus.Ready, ready.Status);
            Assert.True(ready.SharedStorageReady);
            Assert.Equal(SqlServerSharedStateDocumentStore.SupportedSchemaVersion, ready.SchemaVersion);
            Assert.Equal(0, await CountDocumentsAsync(runtimeConnectionString));

            var backend = new SqlServerSharedStateSqlBackend();
            var normal = await backend.CompareExchangeAsync(
                runtimeConnectionString,
                "shared-state:normal",
                0,
                "{\"value\":1}",
                5,
                CancellationToken.None);
            var normalRead = await backend.ReadAsync(
                runtimeConnectionString,
                "shared-state:normal",
                5,
                CancellationToken.None);
            Assert.True(normal.Applied);
            Assert.Equal("{\"value\":1}", normalRead?.PayloadJson);

            const string oversizedKey = "shared-state:oversized";
            var oversizedPayload = "{\"value\":\"" +
                new string('x', SqlServerSharedStateDocumentStore.MaximumPayloadBytes) +
                "\"}";
            await InsertRuntimeDocumentAsync(runtimeConnectionString, oversizedKey, oversizedPayload);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                backend.ReadAsync(runtimeConnectionString, oversizedKey, 5, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                backend.CompareExchangeAsync(
                    runtimeConnectionString,
                    oversizedKey,
                    0,
                    "{}",
                    5,
                    CancellationToken.None));

            var documentCountBeforePermissionDrift = await CountDocumentsAsync(runtimeConnectionString);
            await ExecuteAsync(
                stateAdminConnectionString,
                $"DENY UPDATE ON dbo.MonitorSharedStateDocuments TO {quotedRuntimeLogin};");

            var unavailable = await readinessService.GetAsync();
            Assert.Equal(SharedStateReadinessStatus.Unavailable, unavailable.Status);
            Assert.False(unavailable.SharedStorageReady);
            Assert.Null(unavailable.SchemaVersion);
            Assert.Equal(documentCountBeforePermissionDrift, await CountDocumentsAsync(runtimeConnectionString));
        }
        finally
        {
            await ExecuteAsync(
                masterConnectionString,
                $"ALTER DATABASE {quotedDatabase} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {quotedDatabase};");
        }
    }

    private static async Task InsertRuntimeDocumentAsync(string connectionString, string key, string payloadJson)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT dbo.MonitorSharedStateDocuments (DocumentKey, Version, PayloadJson, UpdatedAtUtc)
            VALUES (@DocumentKey, 1, @PayloadJson, SYSUTCDATETIME());
            """;
        command.Parameters.Add(new SqlParameter("@DocumentKey", SqlDbType.NVarChar, 128) { Value = key });
        command.Parameters.Add(new SqlParameter("@PayloadJson", SqlDbType.NVarChar, -1) { Value = payloadJson });
        await command.ExecuteNonQueryAsync();
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
            ApplicationName = "Monitor.SharedState.RealSql.Tests"
        }.ConnectionString;

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
}
