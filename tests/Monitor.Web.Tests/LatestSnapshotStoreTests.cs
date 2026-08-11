using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class LatestSnapshotStoreTests
{
    [Fact]
    public async Task FileStore_RestartHydratesCache_AndReadViewDoesNotCollectAgain()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "latest-snapshots.json");
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        var registration = Registration(now.AddMinutes(-5));
        var collector = new CountingCollector(Snapshot(registration.Id, now));
        var firstCache = new ServerHealthSnapshotCache(
            collector,
            clock,
            latestSnapshotStore: new FileLatestSnapshotStore(path));

        var collected = await firstCache.GetAsync(registration);
        Assert.Equal(1, collector.CallCount);
        Assert.Equal(SnapshotFreshness.Fresh, collected.Freshness);

        var restartedCollector = new CountingCollector(Snapshot(registration.Id, now.AddSeconds(1)));
        var restartedCache = new ServerHealthSnapshotCache(
            restartedCollector,
            clock,
            latestSnapshotStore: new FileLatestSnapshotStore(path));
        var restartedRepository = new InMemoryServerRegistrationRepository();
        restartedRepository.Upsert(registration);
        var reads = new MonitorReadService(new DemoMonitorService(), restartedRepository, restartedCache);

        var view = await reads.GetServerAsync(registration.Id.ToString("D"));

        Assert.NotNull(view);
        Assert.Equal(ServerDataSource.LiveFresh, view!.Server.Source);
        Assert.Equal("SQL-P0", view.Server.Name);
        Assert.Equal(0, restartedCollector.CallCount);
    }

    [Fact]
    public void FileStore_DoesNotReplaceNewerSnapshotWithOlderEvidence()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "latest-snapshots.json");
        var id = Guid.NewGuid();
        var newer = Snapshot(id, DateTimeOffset.UtcNow);
        var older = newer with { CollectedAtUtc = newer.CollectedAtUtc.AddMinutes(-1), ServerName = "OLDER" };
        var store = new FileLatestSnapshotStore(path);

        store.Upsert(newer);
        store.Upsert(older);
        var restarted = new FileLatestSnapshotStore(path);

        var persisted = Assert.Single(restarted.LoadAll());
        Assert.Equal(newer.ServerName, persisted.ServerName);
        Assert.Equal(newer.CollectedAtUtc, persisted.CollectedAtUtc);
    }

    [Fact]
    public void CorruptLatestSnapshotFile_FailsClosedAtStartup()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "latest-snapshots.json");
        File.WriteAllText(path, "{not-json");

        Assert.Throws<InvalidDataException>(() => new FileLatestSnapshotStore(path));
    }

    private static ServerRegistration Registration(DateTimeOffset createdAt) => new(
        Guid.Parse("61616161-6161-6161-6161-616161616161"),
        "P0 durable target",
        new SqlServerEndpoint("127.0.0.1", 1433, encrypt: true, trustServerCertificate: true),
        SqlAuthenticationMode.SqlLogin,
        new ConnectionSecretReference("local:v1:p0"),
        true,
        createdAt);

    private static ServerHealthSnapshot Snapshot(Guid id, DateTimeOffset collectedAt) => new(
        id,
        "SQL-P0",
        "16.0.1000.6",
        "Developer Edition",
        null,
        120,
        6,
        6,
        collectedAt,
        new MemoryHealthSnapshot(1_000_000, 500_000, 250_000, 25, false, false, "Available"),
        new DatabaseHealthDetailSnapshot(0, 0, 0, 0, 0, 0),
        new BackupHealthSnapshot(1, 1, collectedAt.AddMinutes(-10)),
        new SqlAgentHealthSnapshot(1, 1, 0),
        new StorageHealthSnapshot(1000, 700, 300),
        new BlockingHealthSnapshot(0, 0),
        new PerformanceHealthSnapshot(0, 0, 0));

    private sealed class CountingCollector(ServerHealthSnapshot snapshot) : ISqlServerSnapshotCollector
    {
        public int CallCount { get; private set; }
        public Task<ServerHealthSnapshot> CollectAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(snapshot);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"monitor-latest-snapshot-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, true);
    }
}