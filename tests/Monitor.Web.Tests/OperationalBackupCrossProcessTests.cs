using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class OperationalBackupCrossProcessTests
{
    [Fact]
    public async Task CreateAsync_WaitsForSharedLease_BeforeWriting()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path, CreateState());
        using var heldLease = HoldBackupLease(temp.Path);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var createTask = Task.Run(async () =>
        {
            started.SetResult(true);
            return await service.CreateAsync();
        });

        await started.Task;
        await Task.Delay(150);

        Assert.False(createTask.IsCompleted);
        var backupRoot = Path.Combine(temp.Path, "backups");
        Assert.False(Directory.Exists(backupRoot) && Directory.EnumerateFiles(backupRoot, "monitor-backup-*.json").Any());

        heldLease.Dispose();
        var item = await createTask;

        Assert.True(File.Exists(Path.Combine(backupRoot, $"monitor-backup-{item.BackupId}.json")));
    }

    [Fact]
    public async Task ValidateAsync_WaitsForSharedLease_BeforeReading()
    {
        using var temp = new TempDirectory();
        var state = CreateState();
        var writer = new UnsupportedRestoreWriter();
        var creator = CreateService(temp.Path, state, writer);
        var item = await creator.CreateAsync();
        var peer = CreateService(temp.Path, state, writer);
        using var heldLease = HoldBackupLease(temp.Path);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var validationTask = Task.Run(async () =>
        {
            started.SetResult(true);
            return await peer.ValidateAsync(item.BackupId);
        });

        await started.Task;
        await Task.Delay(150);
        Assert.False(validationTask.IsCompleted);

        heldLease.Dispose();
        var validation = await validationTask;

        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task GetReadiness_WaitsForSharedLease_BeforeScanning()
    {
        using var temp = new TempDirectory();
        var state = CreateState();
        var writer = new UnsupportedRestoreWriter();
        var creator = CreateService(temp.Path, state, writer);
        await creator.CreateAsync();
        var peer = CreateService(temp.Path, state, writer);
        using var heldLease = HoldBackupLease(temp.Path);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var readinessTask = Task.Run(() =>
        {
            started.SetResult(true);
            return peer.GetReadiness();
        });

        await started.Task;
        await Task.Delay(150);
        Assert.False(readinessTask.IsCompleted);

        heldLease.Dispose();
        var readiness = await readinessTask;

        Assert.Equal(1, readiness.BackupCount);
        Assert.Single(readiness.RecentBackups);
    }

    private static FileStream HoldBackupLease(string root)
    {
        var leasePath = Path.Combine(root, "backups") + ".lock";
        Directory.CreateDirectory(Path.GetDirectoryName(leasePath)!);
        return new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private static OperationalBackupService CreateService(
        string root,
        TestState state,
        IOperationalRestoreWriter? writer = null) =>
        new(
            state.Registrations,
            state.Incidents,
            state.History,
            state.Audit,
            writer ?? new UnsupportedRestoreWriter(),
            new BackupStoreOptions { RootPath = "unused", RetentionCount = 10, MaxBundleBytes = 8 * 1024 * 1024 },
            Path.Combine(root, "backups"),
            TimeProvider.System);

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
            new HealthFinding(
                registrationId,
                "database.unavailable",
                FindingSeverity.Critical,
                "Database unavailable",
                "1 database is not online.",
                DateTimeOffset.UtcNow.AddMinutes(-2))
        ]);
        var history = new InMemorySnapshotHistoryStore(TimeProvider.System);
        history.Append(new SnapshotCacheResult(
            new ServerHealthSnapshot(
                registrationId,
                "FINANCE-SQL",
                "16.0",
                "Enterprise",
                null,
                3600,
                5,
                5,
                DateTimeOffset.UtcNow.AddMinutes(-1)),
            SnapshotFreshness.Fresh,
            TimeSpan.Zero));
        var audit = new InMemoryAuditStore(TimeProvider.System);
        audit.Append("Admin", "incident.transition", $"{registrationId:N}:database.unavailable", "Open->Acknowledged");
        return new TestState(registrations, incidents, history, audit);
    }

    private sealed record TestState(
        InMemoryServerRegistrationRepository Registrations,
        InMemoryHealthIncidentRepository Incidents,
        InMemorySnapshotHistoryStore History,
        InMemoryAuditStore Audit);

    private sealed class UnsupportedRestoreWriter : IOperationalRestoreWriter
    {
        public bool IsSupported => false;
        public bool RestartRequired => false;
        public Task RestoreAsync(MonitorBackupBundle bundle, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"monitor-backup-cross-process-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
