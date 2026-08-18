using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedStateReadinessTruthfulnessTests
{
    [Fact]
    public async Task CapabilityProbeFailure_IsUnavailableAndRedacted()
    {
        const string connectionString = "Server=readiness-canary;Database=MonitorState;Password=SECRET-CANARY";
        var options = new SharedStateOptions
        {
            Provider = SharedStateProviderKind.SqlServer,
            ConnectionStringEnvironmentVariable = "MONITOR_SHARED_STATE_SQL_CONNECTION",
            CommandTimeoutSeconds = 5
        };
        var backend = new FailingReadinessBackend("UPDATE permission missing; SECRET-CANARY");
        var store = new SqlServerSharedStateDocumentStore(options, backend, _ => connectionString);
        var service = new SharedStateReadinessService(options, store);

        var readiness = await service.GetAsync();
        var serialized = $"{readiness.Status}|{readiness.Message}";

        Assert.Equal(SharedStateReadinessStatus.Unavailable, readiness.Status);
        Assert.False(readiness.SharedStorageReady);
        Assert.Null(readiness.SchemaVersion);
        Assert.DoesNotContain("SECRET-CANARY", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("readiness-canary", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE permission", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnsupportedReadableSchema_RemainsSchemaMismatch()
    {
        var options = new SharedStateOptions
        {
            Provider = SharedStateProviderKind.SqlServer,
            ConnectionStringEnvironmentVariable = "MONITOR_SHARED_STATE_SQL_CONNECTION",
            CommandTimeoutSeconds = 5
        };
        var backend = new FixedSchemaBackend(2);
        var store = new SqlServerSharedStateDocumentStore(options, backend, _ => "Server=canary;Database=MonitorState;Integrated Security=True");
        var service = new SharedStateReadinessService(options, store);

        var readiness = await service.GetAsync();

        Assert.Equal(SharedStateReadinessStatus.SchemaMismatch, readiness.Status);
        Assert.False(readiness.SharedStorageReady);
        Assert.Equal(2, readiness.SchemaVersion);
    }

    private sealed class FailingReadinessBackend(string message) : ISharedStateSqlBackend
    {
        public Task<int?> ReadSchemaVersionAsync(string connectionString, int commandTimeoutSeconds, CancellationToken cancellationToken) =>
            Task.FromException<int?>(new InvalidOperationException(message));

        public Task<SharedStateDocument?> ReadAsync(string connectionString, string key, int commandTimeoutSeconds, CancellationToken cancellationToken) =>
            Task.FromResult<SharedStateDocument?>(null);

        public Task<SharedStateWriteResult> CompareExchangeAsync(string connectionString, string key, long expectedVersion, string payloadJson, int commandTimeoutSeconds, CancellationToken cancellationToken) =>
            Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
    }

    private sealed class FixedSchemaBackend(int schemaVersion) : ISharedStateSqlBackend
    {
        public Task<int?> ReadSchemaVersionAsync(string connectionString, int commandTimeoutSeconds, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(schemaVersion);

        public Task<SharedStateDocument?> ReadAsync(string connectionString, string key, int commandTimeoutSeconds, CancellationToken cancellationToken) =>
            Task.FromResult<SharedStateDocument?>(null);

        public Task<SharedStateWriteResult> CompareExchangeAsync(string connectionString, string key, long expectedVersion, string payloadJson, int commandTimeoutSeconds, CancellationToken cancellationToken) =>
            Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
    }
}
