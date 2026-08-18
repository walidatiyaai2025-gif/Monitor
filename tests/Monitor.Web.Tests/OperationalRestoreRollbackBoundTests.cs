using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class OperationalRestoreRollbackBoundTests
{
    private const int TestBoundBytes = 2 * 1024;

    [Fact]
    public async Task OversizedExistingRegistration_FailsBeforeOverwrite()
    {
        using var temp = new TempDirectory();
        var registrationPath = Path.Combine(temp.Path, "state", "registrations.json");
        Directory.CreateDirectory(Path.GetDirectoryName(registrationPath)!);
        await using (var stream = new FileStream(registrationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(TestBoundBytes + 1L);
        }
        var writer = CreateWriter(temp.Path);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            writer.RestoreAsync(BundleWithRegistration(), CancellationToken.None));

        Assert.Contains("bounded file size", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TestBoundBytes + 1L, new FileInfo(registrationPath).Length);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "state", "operational")));
    }

    [Fact]
    public async Task OversizedLaterOperationalTarget_RollsBackEarlierRegistrationExactly()
    {
        using var temp = new TempDirectory();
        var registrationPath = Path.Combine(temp.Path, "state", "registrations.json");
        Directory.CreateDirectory(Path.GetDirectoryName(registrationPath)!);
        const string originalRegistration = "{\"version\":1,\"registrations\":[]}";
        await File.WriteAllTextAsync(registrationPath, originalRegistration);

        var operationalRoot = Path.Combine(temp.Path, "state", "operational");
        Directory.CreateDirectory(operationalRoot);
        var auditPath = Path.Combine(operationalRoot, "audit.json");
        await using (var stream = new FileStream(auditPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(TestBoundBytes + 1L);
        }
        var writer = CreateWriter(temp.Path);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            writer.RestoreAsync(BundleWithRegistration(), CancellationToken.None));

        Assert.Contains("bounded file size", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalRegistration, await File.ReadAllTextAsync(registrationPath));
        Assert.Equal(TestBoundBytes + 1L, new FileInfo(auditPath).Length);
        Assert.False(File.Exists(Path.Combine(operationalRoot, "incidents.json")));
        Assert.False(File.Exists(Path.Combine(operationalRoot, "history.json")));
    }

    [Fact]
    public async Task MissingTargets_PreserveExistingRestoreCreationSemantics()
    {
        using var temp = new TempDirectory();
        var writer = CreateWriter(temp.Path);

        await writer.RestoreAsync(BundleWithRegistration(), CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(temp.Path, "state", "registrations.json")));
        Assert.True(File.Exists(Path.Combine(temp.Path, "state", "operational", "audit.json")));
        Assert.True(File.Exists(Path.Combine(temp.Path, "state", "operational", "incidents.json")));
        Assert.True(File.Exists(Path.Combine(temp.Path, "state", "operational", "history.json")));
    }

    private static OperationalRestoreWriter CreateWriter(string contentRoot) => new(
        new RegistrationStoreOptions
        {
            Mode = RegistrationStoreMode.File,
            Path = Path.Combine(contentRoot, "state", "registrations.json")
        },
        new OperationalStoreOptions
        {
            Mode = OperationalStoreMode.File,
            RootPath = Path.Combine(contentRoot, "state", "operational")
        },
        new HaStateOptions(),
        new UnusedSharedStateStore(),
        new InMemoryServerRegistrationRepository(),
        contentRoot,
        Path.Combine(contentRoot, "wwwroot"),
        registrationRollbackSnapshotBytes: TestBoundBytes,
        operationalRollbackSnapshotBytes: TestBoundBytes);

    private static MonitorBackupBundle BundleWithRegistration()
    {
        var registration = new BackupRegistration(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Restored SQL",
            "sql-restored.internal",
            1433,
            null,
            true,
            false,
            SqlAuthenticationMode.IntegratedSecurity,
            null,
            true,
            new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.Zero));

        return new MonitorBackupBundle(
            1,
            "202608181200000-aaaaaaaaaaaaaa",
            new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero),
            new BackupManifest(1, string.Empty, string.Empty, string.Empty, string.Empty),
            [registration],
            [],
            [],
            []);
    }

    private sealed class UnusedSharedStateStore : ISharedStateDocumentStore
    {
        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string key,
            long expectedVersion,
            string payloadJson,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"monitor-restore-bound-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "wwwroot"));
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
