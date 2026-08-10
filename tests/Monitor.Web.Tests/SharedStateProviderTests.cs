using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedStateProviderTests
{
    private const string ConnectionEnvironmentVariable = "MONITOR_SHARED_STATE_SQL_CONNECTION";
    private const string ConnectionCanary = "Server=shared-state-canary;Database=MonitorState;Integrated Security=True";

    [Fact]
    public void DisabledOptions_AreValidWithoutEnvironmentConfiguration()
    {
        var options = new SharedStateOptions();

        var exception = Record.Exception(options.Validate);

        Assert.Null(exception);
        Assert.Equal(SharedStateProviderKind.Disabled, options.Provider);
    }

    [Theory]
    [InlineData("MONITOR SHARED STATE")]
    [InlineData("MONITOR-SHARED-STATE")]
    [InlineData("")]
    [InlineData("9INVALID_START")]
    public void SqlServerOptions_InvalidEnvironmentVariableName_FailsClosed(string variableName)
    {
        var options = SqlOptions();
        options.ConnectionStringEnvironmentVariable = variableName;

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void SqlServerOptions_InvalidCommandTimeout_FailsClosed(int timeoutSeconds)
    {
        var options = SqlOptions();
        options.CommandTimeoutSeconds = timeoutSeconds;

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public async Task MissingConnectionEnvironmentVariable_FailsWithRedactedUnavailableStatus()
    {
        var store = new SqlServerSharedStateDocumentStore(SqlOptions(), new FakeBackend(), _ => null);

        var exception = await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(
            () => store.ReadAsync("registration:estate"));

        Assert.Equal("Shared state provider is unavailable.", exception.Message);
        Assert.DoesNotContain("MONITOR_SHARED_STATE_SQL_CONNECTION", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderError_IsRedactedAndDoesNotExposeConnectionOrBackendMessage()
    {
        var backend = new FakeBackend { ThrowMessage = "Password=CANARY;Server=internal-state-db" };
        var store = Store(backend);

        var exception = await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(
            () => store.ReadAsync("incident:all"));

        Assert.Equal("Shared state provider is unavailable.", exception.Message);
        Assert.DoesNotContain("CANARY", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-state-db", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("shared-state-canary", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad key")]
    [InlineData("bad/key")]
    public async Task InvalidDocumentKey_IsRejectedBeforeProviderCall(string key)
    {
        var backend = new FakeBackend();
        var store = Store(backend);

        await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAsync(key));

        Assert.Equal(0, backend.ReadCalls);
    }

    [Fact]
    public async Task TooLongDocumentKey_IsRejectedBeforeProviderCall()
    {
        var backend = new FakeBackend();
        var store = Store(backend);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.ReadAsync(new string('a', SqlServerSharedStateDocumentStore.MaximumKeyLength + 1)));

        Assert.Equal(0, backend.ReadCalls);
    }

    [Fact]
    public async Task InvalidJsonPayload_IsRejectedBeforeProviderCall()
    {
        var backend = new FakeBackend();
        var store = Store(backend);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CompareExchangeAsync("audit:head", 0, "{ not-json"));

        Assert.Equal(0, backend.WriteCalls);
    }

    [Fact]
    public async Task OversizedPayload_IsRejectedBeforeProviderCall()
    {
        var backend = new FakeBackend();
        var store = Store(backend);
        var payload = "{\"value\":\"" + new string('a', SqlServerSharedStateDocumentStore.MaximumPayloadBytes) + "\"}";

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CompareExchangeAsync("history:estate", 0, payload));

        Assert.Equal(0, backend.WriteCalls);
    }

    [Fact]
    public async Task NegativeExpectedVersion_IsRejectedBeforeProviderCall()
    {
        var backend = new FakeBackend();
        var store = Store(backend);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.CompareExchangeAsync("registration:estate", -1, "{}"));

        Assert.Equal(0, backend.WriteCalls);
    }

    [Fact]
    public async Task TwoStoreInstances_SharedBackend_RejectStaleExpectedVersion()
    {
        var backend = new FakeBackend();
        var first = Store(backend);
        var second = Store(backend);

        var created = await first.CompareExchangeAsync("registration:estate", 0, "{\"servers\":1}");
        var observed = await second.ReadAsync("registration:estate");
        var updated = await first.CompareExchangeAsync("registration:estate", created.Document!.Version, "{\"servers\":2}");
        var stale = await second.CompareExchangeAsync("registration:estate", observed!.Version, "{\"servers\":99}");

        Assert.True(created.Applied);
        Assert.True(updated.Applied);
        Assert.Equal(2, updated.Document!.Version);
        Assert.Equal(SharedStateWriteStatus.Conflict, stale.Status);
        Assert.Equal(2, stale.Document!.Version);
        Assert.Equal("{\"servers\":2}", stale.Document.PayloadJson);
    }

    [Fact]
    public async Task ConnectionString_IsTakenOnlyFromEnvironmentReader()
    {
        var backend = new FakeBackend();
        var store = Store(backend);

        await store.ReadAsync("registration:estate");

        Assert.Equal(ConnectionCanary, backend.LastConnectionString);
    }

    [Fact]
    public async Task Readiness_ReportsOnlyProviderAndSchemaState()
    {
        var backend = new FakeBackend { SchemaVersion = 1 };
        var options = SqlOptions();
        var store = new SqlServerSharedStateDocumentStore(options, backend, _ => ConnectionCanary);
        var service = new SharedStateReadinessService(options, store);

        var readiness = await service.GetAsync();
        var rendered = $"{readiness.Provider}|{readiness.Status}|{readiness.Message}";

        Assert.True(readiness.SharedStorageReady);
        Assert.Equal(1, readiness.SchemaVersion);
        Assert.DoesNotContain("shared-state-canary", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MonitorState", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Readiness_SchemaMismatch_DoesNotClaimSharedStorageReady()
    {
        var backend = new FakeBackend { SchemaVersion = 2 };
        var options = SqlOptions();
        var store = new SqlServerSharedStateDocumentStore(options, backend, _ => ConnectionCanary);
        var service = new SharedStateReadinessService(options, store);

        var readiness = await service.GetAsync();

        Assert.Equal(SharedStateReadinessStatus.SchemaMismatch, readiness.Status);
        Assert.False(readiness.SharedStorageReady);
        Assert.Equal(2, readiness.SchemaVersion);
    }

    [Fact]
    public async Task Readiness_MissingEnvironmentValue_IsSafeUnavailable()
    {
        var options = SqlOptions();
        var store = new SqlServerSharedStateDocumentStore(options, new FakeBackend(), _ => null);
        var service = new SharedStateReadinessService(options, store);

        var readiness = await service.GetAsync();

        Assert.Equal(SharedStateReadinessStatus.Unavailable, readiness.Status);
        Assert.False(readiness.SharedStorageReady);
        Assert.DoesNotContain(ConnectionEnvironmentVariable, readiness.Message, StringComparison.Ordinal);
    }

    private static SharedStateOptions SqlOptions() =>
        new()
        {
            Provider = SharedStateProviderKind.SqlServer,
            ConnectionStringEnvironmentVariable = ConnectionEnvironmentVariable,
            CommandTimeoutSeconds = 5
        };

    private static SqlServerSharedStateDocumentStore Store(FakeBackend backend) =>
        new(SqlOptions(), backend, _ => ConnectionCanary);

    private sealed class FakeBackend : ISharedStateSqlBackend
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);

        public int? SchemaVersion { get; set; } = 1;
        public string? ThrowMessage { get; set; }
        public int ReadCalls { get; private set; }
        public int WriteCalls { get; private set; }
        public string? LastConnectionString { get; private set; }

        public Task<int?> ReadSchemaVersionAsync(
            string connectionString,
            int commandTimeoutSeconds,
            CancellationToken cancellationToken)
        {
            LastConnectionString = connectionString;
            MaybeThrow();
            return Task.FromResult(SchemaVersion);
        }

        public Task<SharedStateDocument?> ReadAsync(
            string connectionString,
            string key,
            int commandTimeoutSeconds,
            CancellationToken cancellationToken)
        {
            LastConnectionString = connectionString;
            MaybeThrow();
            lock (_gate)
            {
                ReadCalls++;
                return Task.FromResult(_documents.TryGetValue(key, out var value) ? value : null);
            }
        }

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string connectionString,
            string key,
            long expectedVersion,
            string payloadJson,
            int commandTimeoutSeconds,
            CancellationToken cancellationToken)
        {
            LastConnectionString = connectionString;
            MaybeThrow();
            lock (_gate)
            {
                WriteCalls++;
                if (!_documents.TryGetValue(key, out var current))
                {
                    if (expectedVersion != 0)
                    {
                        return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
                    }

                    var created = new SharedStateDocument(key, 1, payloadJson, DateTimeOffset.UtcNow);
                    _documents[key] = created;
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, created));
                }

                if (current.Version != expectedVersion)
                {
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                }

                var updated = current with
                {
                    Version = current.Version + 1,
                    PayloadJson = payloadJson,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                _documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }

        private void MaybeThrow()
        {
            if (ThrowMessage is not null)
            {
                throw new InvalidOperationException(ThrowMessage);
            }
        }
    }
}
