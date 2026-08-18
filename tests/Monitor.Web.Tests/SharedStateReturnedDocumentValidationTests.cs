using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedStateReturnedDocumentValidationTests
{
    private const string Key = "registration:estate";
    private const string ConnectionString = "Server=shared-state-validation-canary;Database=MonitorState;Integrated Security=True";

    [Fact]
    public async Task Read_BackendReturnsOversizedPayload_FailsClosedWithRedactedError()
    {
        var backend = new ControlledBackend
        {
            ReadDocument = Document(Key, OversizedJson())
        };
        var store = Store(backend);

        var exception = await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(() => store.ReadAsync(Key));

        Assert.Equal("Shared state provider is unavailable.", exception.Message);
        Assert.DoesNotContain("shared-state-validation-canary", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Read_BackendReturnsInvalidJson_FailsClosed()
    {
        var backend = new ControlledBackend
        {
            ReadDocument = Document(Key, "{not-json")
        };
        var store = Store(backend);

        await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(() => store.ReadAsync(Key));
    }

    [Fact]
    public async Task Read_BackendReturnsWrongKey_FailsClosed()
    {
        var backend = new ControlledBackend
        {
            ReadDocument = Document("registration:other", "{}")
        };
        var store = Store(backend);

        await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(() => store.ReadAsync(Key));
    }

    [Fact]
    public async Task Read_BackendReturnsNonPositiveVersion_FailsClosed()
    {
        var backend = new ControlledBackend
        {
            ReadDocument = Document(Key, "{}") with { Version = 0 }
        };
        var store = Store(backend);

        await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(() => store.ReadAsync(Key));
    }

    [Fact]
    public async Task CompareExchange_ConflictReturnsOversizedPayload_FailsClosed()
    {
        var backend = new ControlledBackend
        {
            WriteResult = new SharedStateWriteResult(
                SharedStateWriteStatus.Conflict,
                Document(Key, OversizedJson()))
        };
        var store = Store(backend);

        await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(() =>
            store.CompareExchangeAsync(Key, 1, "{}"));
    }

    [Fact]
    public async Task CompareExchange_AppliedWithoutDocument_FailsClosed()
    {
        var backend = new ControlledBackend
        {
            WriteResult = new SharedStateWriteResult(SharedStateWriteStatus.Applied, null)
        };
        var store = Store(backend);

        await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(() =>
            store.CompareExchangeAsync(Key, 0, "{}"));
    }

    [Fact]
    public async Task ValidReturnedDocument_PreservesExistingReadAndCasSemantics()
    {
        var read = Document(Key, "{\"value\":1}");
        var applied = read with { Version = 2, PayloadJson = "{\"value\":2}" };
        var backend = new ControlledBackend
        {
            ReadDocument = read,
            WriteResult = new SharedStateWriteResult(SharedStateWriteStatus.Applied, applied)
        };
        var store = Store(backend);

        var observed = await store.ReadAsync(Key);
        var result = await store.CompareExchangeAsync(Key, 1, applied.PayloadJson);

        Assert.Equal(read, observed);
        Assert.True(result.Applied);
        Assert.Equal(applied, result.Document);
    }

    private static SqlServerSharedStateDocumentStore Store(ISharedStateSqlBackend backend) =>
        new(
            new SharedStateOptions
            {
                Provider = SharedStateProviderKind.SqlServer,
                ConnectionStringEnvironmentVariable = "MONITOR_SHARED_STATE_SQL_CONNECTION",
                CommandTimeoutSeconds = 5
            },
            backend,
            _ => ConnectionString);

    private static SharedStateDocument Document(string key, string payload) =>
        new(key, 1, payload, DateTimeOffset.UtcNow);

    private static string OversizedJson() =>
        "{\"value\":\"" + new string('x', SqlServerSharedStateDocumentStore.MaximumPayloadBytes) + "\"}";

    private sealed class ControlledBackend : ISharedStateSqlBackend
    {
        public SharedStateDocument? ReadDocument { get; init; }
        public SharedStateWriteResult WriteResult { get; init; } =
            new(SharedStateWriteStatus.Conflict, null);

        public Task<int?> ReadSchemaVersionAsync(
            string connectionString,
            int commandTimeoutSeconds,
            CancellationToken cancellationToken) => Task.FromResult<int?>(1);

        public Task<SharedStateDocument?> ReadAsync(
            string connectionString,
            string key,
            int commandTimeoutSeconds,
            CancellationToken cancellationToken) => Task.FromResult(ReadDocument);

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string connectionString,
            string key,
            long expectedVersion,
            string payloadJson,
            int commandTimeoutSeconds,
            CancellationToken cancellationToken) => Task.FromResult(WriteResult);
    }
}
