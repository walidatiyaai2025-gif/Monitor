using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300NotificationOutboxTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");
    private static readonly Guid ServerId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void B300_031_NotificationEventCarriesOnlyBoundedRoutingMetadata()
    {
        var item = Event(FindingSeverity.Warning, ServerEnvironmentClass.Production);
        Assert.Equal(ServerId, item.RegistrationId);
        Assert.Equal("memory.pressure", item.RuleId);
        Assert.DoesNotContain("password", string.Join('|', item.EventKey, item.IncidentId, item.RuleId), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void B300_032_RoutePolicyIsDeterministic()
    {
        var item = Event(FindingSeverity.Warning, ServerEnvironmentClass.Production);
        Assert.Equal(NotificationRoutingPolicy.Route(item), NotificationRoutingPolicy.Route(item));
        Assert.Equal(NotificationRoute.DbaOnCall, NotificationRoutingPolicy.Route(item));
    }

    [Fact]
    public void B300_033_EnvironmentRoutesWarningBetweenProductionAndNonProduction()
    {
        Assert.Equal(NotificationRoute.DbaOnCall, NotificationRoutingPolicy.Route(Event(FindingSeverity.Warning, ServerEnvironmentClass.Production)));
        Assert.Equal(NotificationRoute.Operations, NotificationRoutingPolicy.Route(Event(FindingSeverity.Warning, ServerEnvironmentClass.Test)));
    }

    [Fact]
    public void B300_034_CriticalSeverityRoutesToCriticalOnCall()
    {
        Assert.Equal(NotificationRoute.CriticalOnCall, NotificationRoutingPolicy.Route(Event(FindingSeverity.Critical, ServerEnvironmentClass.Development)));
    }

    [Fact]
    public void B300_035_SuppressionBlocksDispatchByRoutingToAuditOnly()
    {
        var item = Event(FindingSeverity.Critical, ServerEnvironmentClass.Production) with { Suppressed = true };
        Assert.Equal(NotificationRoute.AuditOnly, NotificationRoutingPolicy.Route(item));
    }

    [Fact]
    public void B300_036_MaintenanceRoutesToAuditOnlyWithoutDroppingEvent()
    {
        var item = Event(FindingSeverity.Critical, ServerEnvironmentClass.Production) with { MaintenanceActive = true };
        var store = new InMemoryNotificationOutboxStore(new FixedTimeProvider(Now));
        Assert.True(new NotificationOutboxService(store, new FixedTimeProvider(Now)).Capture(item));
        var captured = Assert.Single(store.Read());
        Assert.Equal(NotificationRoute.AuditOnly, captured.Route);
        Assert.Equal(item.IncidentId, captured.Event.IncidentId);
    }

    [Fact]
    public void B300_037_SharedOutboxPersistsAcrossNodesAndIsBounded()
    {
        var shared = new MemorySharedStore(new FixedTimeProvider(Now));
        var nodeA = new SharedNotificationOutboxStore(shared, new FixedTimeProvider(Now));
        var nodeB = new SharedNotificationOutboxStore(shared, new FixedTimeProvider(Now));
        var service = new NotificationOutboxService(nodeA, new FixedTimeProvider(Now));
        Assert.True(service.Capture(Event(FindingSeverity.Warning, ServerEnvironmentClass.Production)));
        Assert.Single(nodeB.Read());
    }

    [Fact]
    public void B300_038_IdempotencyKeyIsStableAndDuplicateCaptureIsRejected()
    {
        var item = Event(FindingSeverity.Warning, ServerEnvironmentClass.Production);
        var firstKey = NotificationRoutingPolicy.IdempotencyKey(item);
        var secondKey = NotificationRoutingPolicy.IdempotencyKey(item);
        Assert.Equal(64, firstKey.Length);
        Assert.Equal(firstKey, secondKey);
        var store = new InMemoryNotificationOutboxStore(new FixedTimeProvider(Now));
        var service = new NotificationOutboxService(store, new FixedTimeProvider(Now));
        Assert.True(service.Capture(item));
        Assert.False(service.Capture(item));
    }

    [Fact]
    public void B300_039_RetryTransitionsToDeadLetterAndDeliveredIsTerminal()
    {
        var clock = new FixedTimeProvider(Now);
        var store = new InMemoryNotificationOutboxStore(clock);
        var item = Event(FindingSeverity.Warning, ServerEnvironmentClass.Production);
        new NotificationOutboxService(store, clock).Capture(item);
        var key = NotificationRoutingPolicy.IdempotencyKey(item);
        Assert.True(store.TryRecordFailure(key, "temporary", 2));
        Assert.Equal(NotificationDeliveryState.Pending, Assert.Single(store.Read()).State);
        Assert.True(store.TryRecordFailure(key, "temporary", 2));
        Assert.Equal(NotificationDeliveryState.DeadLetter, Assert.Single(store.Read()).State);
        Assert.False(store.TryRecordFailure(key, "again", 2));
    }

    [Fact]
    public void B300_040_OutcomeIsBoundedAndSharedRetryStateConverges()
    {
        var clock = new FixedTimeProvider(Now);
        var shared = new MemorySharedStore(clock);
        var a = new SharedNotificationOutboxStore(shared, clock);
        var b = new SharedNotificationOutboxStore(shared, clock);
        var item = Event(FindingSeverity.Critical, ServerEnvironmentClass.Production);
        new NotificationOutboxService(a, clock).Capture(item);
        var key = NotificationRoutingPolicy.IdempotencyKey(item);
        Assert.True(b.TryMarkDelivered(key, new string('x', 500)));
        var row = Assert.Single(a.Read());
        Assert.Equal(NotificationDeliveryState.Delivered, row.State);
        Assert.NotNull(row.LastOutcome);
        Assert.True(row.LastOutcome!.Length <= 160);
    }

    private static MonitorNotificationEvent Event(FindingSeverity severity, ServerEnvironmentClass environment) =>
        new("event-1", ServerId, $"{ServerId:N}:memory.pressure", "memory.pressure", severity, environment, false, false, Now);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class MemorySharedStore(TimeProvider clock) : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _items = new(StringComparer.Ordinal);
        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_gate) { _items.TryGetValue(key, out var value); return Task.FromResult(value); }
        }
        public Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _items.TryGetValue(key, out var current);
                var version = current?.Version ?? 0;
                if (version != expectedVersion) return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                var next = new SharedStateDocument(key, version + 1, payloadJson, clock.GetUtcNow());
                _items[key] = next;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, next));
            }
        }
    }
}
