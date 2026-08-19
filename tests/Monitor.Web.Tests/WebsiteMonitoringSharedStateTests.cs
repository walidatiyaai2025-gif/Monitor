using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class WebsiteMonitoringSharedStateTests
{
    [Fact]
    public void TargetAndCheckState_AreVisibleAcrossIndependentStoreInstances()
    {
        var shared = new TestSharedStateStore();
        var targetsA = new SharedWebsiteTargetStore(shared);
        var targetsB = new SharedWebsiteTargetStore(shared);
        var checksA = new SharedWebsiteCheckStateStore(shared);
        var checksB = new SharedWebsiteCheckStateStore(shared);
        var target = Target();
        var observedAt = DateTimeOffset.Parse("2026-08-19T10:00:00Z");

        targetsA.Upsert(target);
        checksA.Upsert(new WebsiteCheckState(target.Id, WebsiteProbeState.Down, "http.5xx", 2, 0, observedAt, null, observedAt));

        Assert.Equal(target, targetsB.Get(target.Id));
        Assert.Equal(WebsiteProbeState.Down, checksB.Get(target.Id)!.LastState);

        Assert.True(targetsB.Remove(target.Id));
        Assert.Null(targetsA.Get(target.Id));
    }

    [Fact]
    public void ScheduleClaim_IsAtomicAcrossIndependentStoreInstances()
    {
        var shared = new TestSharedStateStore();
        var first = new SharedWebsiteScheduleStateStore(shared);
        var second = new SharedWebsiteScheduleStateStore(shared);
        var targetId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-19T10:00:00Z");
        var interval = TimeSpan.FromMinutes(1);

        var claim = first.TryClaim(targetId, now, interval, TimeSpan.FromSeconds(30));

        Assert.NotNull(claim);
        Assert.Null(second.TryClaim(targetId, now, interval, TimeSpan.FromSeconds(30)));
        Assert.True(second.Complete(claim!, now.AddSeconds(5), interval));
        Assert.Null(first.TryClaim(targetId, now.AddSeconds(30), interval, TimeSpan.FromSeconds(30)));
        Assert.NotNull(first.TryClaim(targetId, now.AddSeconds(66), interval, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void HistoryAndNotificationGroups_AreSharedAcrossInstances()
    {
        var shared = new TestSharedStateStore();
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-19T10:00:00Z"));
        var historyA = new SharedWebsiteProbeHistoryStore(shared, time);
        var historyB = new SharedWebsiteProbeHistoryStore(shared, time);
        var groupsA = new SharedWebsiteNotificationGroupStore(shared);
        var groupsB = new SharedWebsiteNotificationGroupStore(shared);
        var target = Target();
        var result = SuccessfulResult(target, time.GetUtcNow());

        historyA.Append(result);
        groupsA.Upsert(new WebsiteNotificationGroup("ops", "Operations", ["ops@example.com"]));

        Assert.Single(historyB.Read(target.Id, TimeSpan.FromHours(1)));
        Assert.Equal("Operations", groupsB.Get("OPS")!.Name);
        Assert.True(groupsB.Remove("OPS"));
        Assert.Null(groupsA.Get("ops"));
    }

    [Fact]
    public void Outbox_DeduplicatesAndClaimsAcrossIndependentStoreInstances()
    {
        var shared = new TestSharedStateStore();
        var first = new SharedWebsiteNotificationOutbox(shared);
        var second = new SharedWebsiteNotificationOutbox(shared);
        var now = DateTimeOffset.Parse("2026-08-19T10:00:00Z");
        var item = Notification("item-a", "dedup-1", now);

        Assert.True(first.Enqueue(item));
        Assert.False(second.Enqueue(Notification("item-b", "dedup-1", now)));

        var claim = first.TryClaimDue(now, TimeSpan.FromSeconds(30));
        Assert.NotNull(claim);
        Assert.Null(second.TryClaimDue(now, TimeSpan.FromSeconds(30)));
        Assert.True(second.MarkSent(claim!, now.AddSeconds(2)));

        var snapshot = first.Snapshot();
        Assert.Single(snapshot);
        Assert.Equal(WebsiteNotificationDeliveryStatus.Sent, snapshot[0].Status);
        Assert.Null(snapshot[0].LeaseToken);
    }

    [Fact]
    public async Task DistributedProbeLease_AllowsOnlyOneNodeForTheSameTarget()
    {
        var shared = new TestSharedStateStore();
        var options = new DistributedCoordinationOptions { Enabled = true, MaxConflictRetries = 12 };
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-19T10:00:00Z"));
        var first = new SharedStateDistributedLeaseManager(shared, new NodeIdentity("node-a"), time, options);
        var second = new SharedStateDistributedLeaseManager(shared, new NodeIdentity("node-b"), time, options);
        var resource = $"website.probe.{Guid.NewGuid():N}";

        var lease = await first.TryAcquireAsync(resource, TimeSpan.FromSeconds(120));

        Assert.NotNull(lease);
        Assert.Null(await second.TryAcquireAsync(resource, TimeSpan.FromSeconds(120)));
        Assert.True(await first.ReleaseAsync(lease!));
        Assert.NotNull(await second.TryAcquireAsync(resource, TimeSpan.FromSeconds(120)));
    }

    [Fact]
    public void Registration_UsesSharedStoresAndRequiresCoordinationForSharedActivation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WebsiteMonitoring:Enabled"] = "true",
            ["WebsiteNotifications:Enabled"] = "false"
        }).Build();
        var topology = new DeploymentTopologyOptions { Mode = DeploymentTopology.MultiNode };
        var coordination = new DistributedCoordinationOptions { Enabled = true };
        var shared = new TestSharedStateStore();
        var services = new ServiceCollection();
        services.AddSingleton<ISharedStateDocumentStore>(shared);
        services.AddSingleton(TimeProvider.System);

        services.AddWebsiteMonitoringSubsystem(configuration, topology, coordination, useSharedOperationalState: true, operationalRoot: null);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<SharedWebsiteTargetStore>(provider.GetRequiredService<IWebsiteTargetStore>());
        Assert.IsType<SharedWebsiteProbeHistoryStore>(provider.GetRequiredService<IWebsiteProbeHistoryStore>());
        Assert.IsType<SharedWebsiteScheduleStateStore>(provider.GetRequiredService<IWebsiteScheduleStateStore>());
        Assert.IsType<SharedWebsiteCheckStateStore>(provider.GetRequiredService<IWebsiteCheckStateStore>());
        Assert.IsType<SharedWebsiteNotificationGroupStore>(provider.GetRequiredService<IWebsiteNotificationGroupStore>());
        Assert.IsType<SharedWebsiteNotificationOutbox>(provider.GetRequiredService<IWebsiteNotificationOutbox>());

        var noCoordination = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => noCoordination.AddWebsiteMonitoringSubsystem(
            configuration,
            topology,
            new DistributedCoordinationOptions { Enabled = false },
            useSharedOperationalState: true,
            operationalRoot: null));

        var localState = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => localState.AddWebsiteMonitoringSubsystem(
            configuration,
            topology,
            coordination,
            useSharedOperationalState: false,
            operationalRoot: null));
    }

    private static WebsiteTargetDefinition Target() => new(
        Guid.NewGuid(),
        "Portal",
        "https://example.com/health",
        "production",
        IntervalSeconds: 60,
        TimeoutSeconds: 10);

    private static WebsiteProbeResult SuccessfulResult(WebsiteTargetDefinition target, DateTimeOffset completedAt) => new(
        target.Id,
        completedAt.AddMilliseconds(-25),
        completedAt,
        new Uri(target.Url),
        new Uri(target.Url),
        0,
        new WebsiteProbeEvidence(true, true, true, false, 200, true, true, null, false, 25, target.SlowThresholdMilliseconds),
        new WebsiteProbeClassification(WebsiteProbeState.Up, "website.available", "End-to-end HTTP path", "high", "The configured website contract was satisfied."),
        completedAt.AddDays(90),
        "CN=example.com",
        "CN=Example CA");

    private static WebsiteNotificationOutboxItem Notification(string id, string dedup, DateTimeOffset now) => new(
        id,
        dedup,
        Guid.NewGuid(),
        $"{Guid.NewGuid():N}:http.5xx",
        WebsiteNotificationKind.IncidentOpened,
        ["ops@example.com"],
        "[ALERT] Portal",
        "Bounded notification body",
        now,
        now,
        0,
        WebsiteNotificationDeliveryStatus.Pending,
        null,
        null,
        null);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestSharedStateStore : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);

        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate) return Task.FromResult(_documents.TryGetValue(key, out var document) ? document : null);
        }

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string key,
            long expectedVersion,
            string payloadJson,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                var currentVersion = _documents.TryGetValue(key, out var current) ? current.Version : 0;
                if (currentVersion != expectedVersion)
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                var next = new SharedStateDocument(key, currentVersion + 1, payloadJson, DateTimeOffset.Parse("2026-08-19T10:00:00Z"));
                _documents[key] = next;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, next));
            }
        }
    }
}
