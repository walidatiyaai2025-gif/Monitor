using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class OperationalBackupServiceTests : IDisposable
{
    [Fact]
    public async Task CreateAndValidate_ExportsAllSafeSections_WithChecksumsAndNoSecretPayloads()
    {
        using var temp = new TempDirectory();
        var state = CreateState();
        var service = CreateService(temp.Path, state, new UnsupportedRestoreWriter());

        var item = await service.CreateAsync();
        var validation = await service.ValidateAsync(item.BackupId);
        var text = await File.ReadAllTextAsync(Path.Combine(temp.Path, "backups", $"monitor-backup-{item.BackupId}.json"));

        Assert.True(validation.IsValid);
        Assert.Single(validation.Bundle!.Registrations);
        Assert.Single(validation.Bundle.Incidents);
        Assert.Single(validation.Bundle.History);
        Assert.Single(validation.Bundle.Audit);
        Assert.Contains("env:FINANCE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("password-canary", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username-canary", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ciphertext", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(64, validation.Bundle.Manifest.RegistrationsSha256.Length);
    }

    [Fact]
    public async Task TamperedBundle_FailsDryRunWithoutCallingRestore()
    {
        using var temp = new TempDirectory();
        var state = CreateState();
        var writer = new TrackingRestoreWriter();
        var service = CreateService(temp.Path, state, writer);
        var item = await service.CreateAsync();
        var path = Path.Combine(temp.Path, "backups", $"monitor-backup-{item.BackupId}.json");
        var text = await File.ReadAllTextAsync(path);
        text = text.Replace("Finance", "Finance-Tampered", StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, text);

        var validation = await service.ValidateAsync(item.BackupId);
        var restore = await service.RestoreAsync(item.BackupId);

        Assert.Equal(BackupValidationStatus.Invalid, validation.Status);
        Assert.Equal(BackupRestoreStatus.ValidationFailed, restore.Status);
        Assert.Equal(0, writer.Calls);
    }

    [Fact]
    public async Task Retention_PrunesOldestBackupFiles()
    {
        using var temp = new TempDirectory();
        var state = CreateState();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(temp.Path, state, new UnsupportedRestoreWriter(), clock, retention: 2);

        await service.CreateAsync();
        clock.Advance(TimeSpan.FromSeconds(1));
        await service.CreateAsync();
        clock.Advance(TimeSpan.FromSeconds(1));
        await service.CreateAsync();

        Assert.Equal(2, Directory.GetFiles(Path.Combine(temp.Path, "backups"), "monitor-backup-*.json").Length);
        Assert.Equal(2, service.GetReadiness().BackupCount);
    }

    [Fact]
    public async Task LocalRestore_WritesNativeFilesAndRequiresRestart()
    {
        using var temp = new TempDirectory();
        var state = CreateState();
        var registrationOptions = new RegistrationStoreOptions { Mode = RegistrationStoreMode.File, Path = Path.Combine(temp.Path, "state", "registrations.json") };
        var operationalOptions = new OperationalStoreOptions { Mode = OperationalStoreMode.File, RootPath = Path.Combine(temp.Path, "state", "operational") };
        var writer = new OperationalRestoreWriter(
            registrationOptions,
            operationalOptions,
            new HaStateOptions(),
            new MemoryDocumentStore(),
            state.Registrations,
            temp.Path,
            Path.Combine(temp.Path, "wwwroot"));
        var service = CreateService(temp.Path, state, writer);
        var item = await service.CreateAsync();

        var restore = await service.RestoreAsync(item.BackupId);

        Assert.True(restore.Succeeded);
        Assert.True(restore.RestartRequired);
        var registrations = new FileServerRegistrationRepository(registrationOptions.Path);
        var audit = new FileAuditStore(Path.Combine(operationalOptions.RootPath, "audit.json"), TimeProvider.System);
        var incidents = new FileHealthIncidentRepository(Path.Combine(operationalOptions.RootPath, "incidents.json"));
        var history = new FileSnapshotHistoryStore(Path.Combine(operationalOptions.RootPath, "history.json"), TimeProvider.System);
        Assert.Single(registrations.GetAll());
        Assert.Single(audit.Read(0, 10));
        Assert.Single(incidents.GetAll());
        Assert.Single(history.Read(state.Registration.Id, TimeSpan.FromHours(24)));
    }

    [Fact]
    public async Task SharedRestore_ConflictRollsBackEarlierDocuments()
    {
        using var temp = new TempDirectory();
        var state = CreateState();
        var shared = new ConflictOnKeyDocumentStore("monitor:incidents:v1");
        await shared.CompareExchangeAsync("monitor:registrations:v1", 0, "{\"version\":1,\"registrations\":[]}");
        await shared.CompareExchangeAsync("monitor:audit:v1", 0, "{\"version\":1,\"events\":[]}");
        var originalRegistration = (await shared.ReadAsync("monitor:registrations:v1"))!.PayloadJson;
        var originalAudit = (await shared.ReadAsync("monitor:audit:v1"))!.PayloadJson;
        var writer = new OperationalRestoreWriter(
            new RegistrationStoreOptions { Mode = RegistrationStoreMode.File, Path = Path.Combine(temp.Path, "unused-registration.json") },
            new OperationalStoreOptions { Mode = OperationalStoreMode.File, RootPath = Path.Combine(temp.Path, "unused-operational") },
            new HaStateOptions { UseSharedRegistrations = true, UseSharedOperationalState = true },
            shared,
            state.Registrations,
            temp.Path,
            Path.Combine(temp.Path, "wwwroot"));
        var service = CreateService(temp.Path, state, writer);
        var item = await service.CreateAsync();
        shared.EnableConflict();

        var result = await service.RestoreAsync(item.BackupId);

        Assert.Equal(BackupRestoreStatus.Failed, result.Status);
        Assert.Equal(originalRegistration, (await shared.ReadAsync("monitor:registrations:v1"))!.PayloadJson);
        Assert.Equal(originalAudit, (await shared.ReadAsync("monitor:audit:v1"))!.PayloadJson);
    }

    [Fact]
    public async Task InMemoryPersistence_IsExportableButRestoreIsExplicitlyUnsupported()
    {
        using var temp = new TempDirectory();
        var state = CreateState();
        var writer = new OperationalRestoreWriter(
            new RegistrationStoreOptions { Mode = RegistrationStoreMode.InMemory },
            new OperationalStoreOptions { Mode = OperationalStoreMode.InMemory },
            new HaStateOptions(),
            new MemoryDocumentStore(),
            state.Registrations,
            temp.Path,
            Path.Combine(temp.Path, "wwwroot"));
        var service = CreateService(temp.Path, state, writer);
        var item = await service.CreateAsync();

        var result = await service.RestoreAsync(item.BackupId);

        Assert.Equal(BackupRestoreStatus.Unsupported, result.Status);
        Assert.False(service.GetReadiness().Ready);
    }

    [Fact]
    public async Task UnknownOrTraversalBackupId_IsRejected()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path, CreateState(), new UnsupportedRestoreWriter());

        Assert.Equal(BackupValidationStatus.Invalid, (await service.ValidateAsync("../../secrets.json")).Status);
        Assert.Equal(BackupValidationStatus.NotFound, (await service.ValidateAsync("202608101200000-aaaaaaaaaaaaaa")).Status);
    }

    [Fact]
    public async Task OversizedBackupFile_IsRejectedBeforeDeserialization()
    {
        using var temp = new TempDirectory();
        const int maxBundleBytes = 64 * 1024;
        const string backupId = "202608101200000-aaaaaaaaaaaaaa";
        var service = CreateService(
            temp.Path,
            CreateState(),
            new UnsupportedRestoreWriter(),
            maxBundleBytes: maxBundleBytes);
        var backupRoot = Path.Combine(temp.Path, "backups");
        Directory.CreateDirectory(backupRoot);
        var path = Path.Combine(backupRoot, $"monitor-backup-{backupId}.json");
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(maxBundleBytes + 1L);
        }

        var validation = await service.ValidateAsync(backupId);

        Assert.Equal(BackupValidationStatus.Invalid, validation.Status);
        Assert.Equal("Backup file size is invalid.", validation.Message);
        Assert.Null(validation.Bundle);
    }

    [Fact]
    public void BackupOptions_EnforceRetentionAndBundleBounds()
    {
        Assert.Throws<InvalidOperationException>(() => new BackupStoreOptions { RetentionCount = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new BackupStoreOptions { MaxBundleBytes = 1 }.Validate());
        new BackupStoreOptions().Validate();
    }

    private static TestState CreateState()
    {
        var registrationId = Guid.NewGuid();
        var registration = new ServerRegistration(
            registrationId,
            "Finance",
            new SqlServerEndpoint("finance-sql.internal", 1433),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference("env:FINANCE"),
            true,
            DateTimeOffset.UtcNow.AddHours(-2));
        var registrations = new InMemoryServerRegistrationRepository();
        registrations.Upsert(registration);
        var incidents = new InMemoryHealthIncidentRepository();
        incidents.Apply([
            new HealthFinding(registrationId, "database.unavailable", FindingSeverity.Critical, "Database unavailable", "1 database is not online.", DateTimeOffset.UtcNow.AddMinutes(-2))
        ]);
        var history = new InMemorySnapshotHistoryStore(TimeProvider.System);
        history.Append(new SnapshotCacheResult(
            new ServerHealthSnapshot(registrationId, "FINANCE-SQL", "16.0", "Enterprise", null, 3600, 5, 5, DateTimeOffset.UtcNow.AddMinutes(-1)),
            SnapshotFreshness.Fresh,
            TimeSpan.Zero));
        var audit = new InMemoryAuditStore(TimeProvider.System);
        audit.Append("Admin", "incident.transition", $"{registrationId:N}:database.unavailable", "Open->Acknowledged");
        return new TestState(registrations, incidents, history, audit, registration);
    }

    private static OperationalBackupService CreateService(
        string root,
        TestState state,
        IOperationalRestoreWriter writer,
        TimeProvider? clock = null,
        int retention = 10,
        int maxBundleBytes = 8 * 1024 * 1024) =>
        new(
            state.Registrations,
            state.Incidents,
            state.History,
            state.Audit,
            writer,
            new BackupStoreOptions { RootPath = "unused", RetentionCount = retention, MaxBundleBytes = maxBundleBytes },
            Path.Combine(root, "backups"),
            clock ?? TimeProvider.System);

    private sealed record TestState(
        InMemoryServerRegistrationRepository Registrations,
        InMemoryHealthIncidentRepository Incidents,
        InMemorySnapshotHistoryStore History,
        InMemoryAuditStore Audit,
        ServerRegistration Registration);

    private sealed class UnsupportedRestoreWriter : IOperationalRestoreWriter
    {
        public bool IsSupported => false;
        public bool RestartRequired => false;
        public Task RestoreAsync(MonitorBackupBundle bundle, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TrackingRestoreWriter : IOperationalRestoreWriter
    {
        public int Calls { get; private set; }
        public bool IsSupported => true;
        public bool RestartRequired => false;
        public Task RestoreAsync(MonitorBackupBundle bundle, CancellationToken cancellationToken) { Calls++; return Task.CompletedTask; }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }

    private class MemoryDocumentStore : ISharedStateDocumentStore
    {
        protected readonly object Gate = new();
        protected readonly Dictionary<string, SharedStateDocument> Documents = new(StringComparer.Ordinal);
        public virtual Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (Gate) return Task.FromResult(Documents.TryGetValue(key, out var value) ? value : null);
        }
        public virtual Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default)
        {
            lock (Gate)
            {
                if (!Documents.TryGetValue(key, out var current))
                {
                    if (expectedVersion != 0) return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
                    var created = new SharedStateDocument(key, 1, payloadJson, DateTimeOffset.UtcNow);
                    Documents[key] = created;
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, created));
                }
                if (current.Version != expectedVersion) return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                var updated = current with { Version = current.Version + 1, PayloadJson = payloadJson, UpdatedAtUtc = DateTimeOffset.UtcNow };
                Documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }
    }

    private sealed class ConflictOnKeyDocumentStore(string conflictKey) : MemoryDocumentStore
    {
        private bool _enabled;
        public void EnableConflict() => _enabled = true;
        public override Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default)
        {
            if (_enabled && string.Equals(key, conflictKey, StringComparison.Ordinal))
            {
                _enabled = false;
                lock (Gate) return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, Documents.TryGetValue(key, out var current) ? current : null));
            }
            return base.CompareExchangeAsync(key, expectedVersion, payloadJson, cancellationToken);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"monitor-backup-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "wwwroot"));
        }
        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
