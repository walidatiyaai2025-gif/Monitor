using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DurableOperationalStoresTests : IDisposable
{
    private static readonly Guid RegistrationId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 11, 0, 0, TimeSpan.Zero);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"monitor-operational-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Audit_RestartPreservesBoundedAppendOnlyEventsAndOrdering()
    {
        var path = Path.Combine(_directory, "audit.json");
        var clock = new MutableClock(Now);
        var first = new FileAuditStore(path, clock);
        first.Append(new string('a', 150), "incident.transition", "incident-1", "Open->Acknowledged");
        clock.Now = Now.AddMinutes(1);
        first.Append("operator.two", "advisor.request", "incident-2", "Ready");

        var restarted = new FileAuditStore(path, clock);
        var events = restarted.Read(0, 100);

        Assert.Equal(2, events.Count);
        Assert.Equal("operator.two", events[0].Actor);
        Assert.Equal("incident-2", events[0].Target);
        Assert.Equal(100, events[1].Actor.Length);
        Assert.Equal("Open->Acknowledged", events[1].Outcome);
    }

    [Fact]
    public void History_RestartPreservesDedupeRetentionAndAllowlistedAggregates()
    {
        var path = Path.Combine(_directory, "history.json");
        var clock = new MutableClock(Now);
        var first = new FileSnapshotHistoryStore(path, clock);
        first.Append(Result(Now.AddHours(-25)));
        var recent = Result(Now.AddMinutes(-5));
        first.Append(recent);
        first.Append(recent);

        var restarted = new FileSnapshotHistoryStore(path, clock);
        var point = Assert.Single(restarted.Read(RegistrationId, TimeSpan.FromHours(24)));

        Assert.Equal(10, point.DatabaseTotal);
        Assert.Equal(10, point.DatabaseOnline);
        Assert.Equal(85, point.MemoryPercent);
        Assert.Equal(2, point.BlockedRequests);
        Assert.Equal(3, point.RunnableTasks);
    }

    [Fact]
    public void Incidents_RestartPreservesIdentityStatusAndFreshReconciliation()
    {
        var path = Path.Combine(_directory, "incidents.json");
        var first = new FileHealthIncidentRepository(path);
        var finding = new HealthFinding(
            RegistrationId,
            "backup.full-gap",
            FindingSeverity.Warning,
            "Full backup gap",
            "2 database(s) have no full backup in 24 hours.",
            Now);
        first.Apply([finding]);
        var incident = Assert.Single(first.GetAll());
        Assert.True(first.TrySetStatus(incident.Id, IncidentStatus.Open, IncidentStatus.Acknowledged));
        Assert.False(first.TrySetStatus(incident.Id, IncidentStatus.Open, IncidentStatus.Resolved));

        var restarted = new FileHealthIncidentRepository(path);
        var loaded = Assert.Single(restarted.GetAll());
        Assert.Equal(incident.Id, loaded.Id);
        Assert.Equal(IncidentStatus.Acknowledged, loaded.Status);
        Assert.Equal(1, loaded.Occurrences);

        restarted.Reconcile(RegistrationId, Now.AddMinutes(1), [], canResolve: true);
        var restartedAgain = new FileHealthIncidentRepository(path);
        Assert.Equal(IncidentStatus.Resolved, Assert.Single(restartedAgain.GetAll()).Status);
    }

    [Theory]
    [InlineData("audit")]
    [InlineData("history")]
    [InlineData("incidents")]
    public void CorruptOperationalFile_FailsClosed(string store)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, $"{store}.json");
        File.WriteAllText(path, "{ definitely-not-json");

        Assert.Throws<InvalidDataException>(() => store switch
        {
            "audit" => new FileAuditStore(path, new MutableClock(Now)),
            "history" => new FileSnapshotHistoryStore(path, new MutableClock(Now)),
            "incidents" => new FileHealthIncidentRepository(path),
            _ => throw new InvalidOperationException()
        });
    }

    [Fact]
    public void OperationalRoot_RejectsPathInsideWebRoot()
    {
        var contentRoot = Path.Combine(_directory, "app");
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);

        Assert.Throws<InvalidOperationException>(() =>
            OperationalStorePath.ResolveOutsideWebRoot("wwwroot/state", contentRoot, webRoot));

        var safe = OperationalStorePath.ResolveOutsideWebRoot("App_Data/operational", contentRoot, webRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(contentRoot, "App_Data/operational")), safe);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static SnapshotCacheResult Result(DateTimeOffset collectedAt) => new(
        new ServerHealthSnapshot(
            RegistrationId,
            "SQL",
            "17",
            "Enterprise",
            null,
            100,
            10,
            10,
            collectedAt,
            new MemoryHealthSnapshot(1000, 200, 500, 85, false, false, "Available"),
            Blocking: new BlockingHealthSnapshot(2, 500),
            Performance: new PerformanceHealthSnapshot(3, 1, 0)),
        SnapshotFreshness.Fresh,
        TimeSpan.Zero);

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
