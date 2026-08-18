using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedStateKeyIdentityTests
{
    [Fact]
    public async Task ExactMixedCaseKey_RemainsSupported()
    {
        var backend = new IdentityBackend
        {
            ReadDocument = new SharedStateDocument(
                "monitor:lease:v1:MixedCase",
                1,
                "{}",
                DateTimeOffset.UtcNow)
        };
        var store = CreateStore(backend);

        var document = await store.ReadAsync("monitor:lease:v1:MixedCase");

        Assert.NotNull(document);
        Assert.Equal("monitor:lease:v1:MixedCase", document.Key);
    }

    [Fact]
    public async Task ReadBackendKeyAlias_IsRedactedAsUnavailable()
    {
        var backend = new IdentityBackend
        {
            ReadDocument = new SharedStateDocument(
                "monitor:lease:v1:MixedCase",
                1,
                "{}",
                DateTimeOffset.UtcNow)
        };
        var store = CreateStore(backend);

        await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(
            () => store.ReadAsync("monitor:lease:v1:mixedcase"));
    }

    [Fact]
    public async Task CompareExchangeBackendKeyAlias_IsRedactedAsUnavailable()
    {
        var backend = new IdentityBackend
        {
            WriteResult = new SharedStateWriteResult(
                SharedStateWriteStatus.Applied,
                new SharedStateDocument(
                    "monitor:lease:v1:MixedCase",
                    2,
                    "{\"value\":2}",
                    DateTimeOffset.UtcNow))
        };
        var store = CreateStore(backend);

        await Assert.ThrowsAsync<SharedStateStoreUnavailableException>(
            () => store.CompareExchangeAsync(
                "monitor:lease:v1:mixedcase",
                1,
                "{\"value\":2}"));
    }

    [Fact]
    public void SqlBackend_UsesPersistedKeyAndByteExactPreMutationGuard()
    {
        var source = ReadRepositoryFile("src", "Monitor.Web", "Services", "SharedStateStore.cs");

        Assert.Contains("DECLARE @LockedDocumentKey nvarchar(128) = NULL;", source, StringComparison.Ordinal);
        Assert.Contains("@LockedDocumentKey = DocumentKey", source, StringComparison.Ordinal);
        Assert.Contains(
            "CONVERT(varbinary(256), @LockedDocumentKey) <> CONVERT(varbinary(256), @DocumentKey)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("THROW 51024, 'Monitor shared-state document key identity is not exact.'", source, StringComparison.Ordinal);
        Assert.Contains("DocumentKey nvarchar(128) NULL", source, StringComparison.Ordinal);
        Assert.Contains("SELECT Applied, DocumentKey, Version, PayloadJson, PayloadStorageBytes, UpdatedAtUtc", source, StringComparison.Ordinal);
        Assert.Contains("keyOrdinal: 0", source, StringComparison.Ordinal);
        Assert.Contains("keyOrdinal: 1", source, StringComparison.Ordinal);
        Assert.Contains("reader.GetString(keyOrdinal)", source, StringComparison.Ordinal);
        Assert.Contains("EnsureExactPersistedKey(key, result.Document);", source, StringComparison.Ordinal);
        Assert.Contains("EnsureExactPersistedKey(key, document);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new SharedStateDocument(\n            key,", source, StringComparison.Ordinal);
    }

    private static SqlServerSharedStateDocumentStore CreateStore(IdentityBackend backend) =>
        new(
            new SharedStateOptions
            {
                Provider = SharedStateProviderKind.SqlServer,
                ConnectionStringEnvironmentVariable = "MONITOR_TEST_SHARED_STATE",
                CommandTimeoutSeconds = 5
            },
            backend,
            _ => "Server=fake;Database=fake;");

    private static string ReadRepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException($"Repository file was not found: {string.Join('/', pathSegments)}");
    }

    private sealed class IdentityBackend : ISharedStateSqlBackend
    {
        public SharedStateDocument? ReadDocument { get; init; }
        public SharedStateWriteResult? WriteResult { get; init; }

        public Task<int?> ReadSchemaVersionAsync(
            string connectionString,
            int commandTimeoutSeconds,
            CancellationToken cancellationToken) =>
            Task.FromResult<int?>(SqlServerSharedStateDocumentStore.SupportedSchemaVersion);

        public Task<SharedStateDocument?> ReadAsync(
            string connectionString,
            string key,
            int commandTimeoutSeconds,
            CancellationToken cancellationToken) =>
            Task.FromResult(ReadDocument);

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string connectionString,
            string key,
            long expectedVersion,
            string payloadJson,
            int commandTimeoutSeconds,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                WriteResult ??
                new SharedStateWriteResult(
                    SharedStateWriteStatus.Conflict,
                    null));
    }
}
