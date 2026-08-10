using System.Text.Json;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class OperatorAuditTrailTests
{
    [Fact]
    public void Trail_IsBoundedAndReturnsNewestFirst()
    {
        var trail = new InMemoryOperatorAuditTrail(3);
        var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

        for (var index = 0; index < 4; index++)
        {
            trail.Record(Event(index, start.AddMinutes(index)));
        }

        var events = trail.GetRecent(100);

        Assert.Equal(3, events.Count);
        Assert.Equal("incident-3", events[0].ResourceId);
        Assert.Equal("incident-2", events[1].ResourceId);
        Assert.Equal("incident-1", events[2].ResourceId);
        Assert.DoesNotContain(events, item => item.ResourceId == "incident-0");
    }

    [Fact]
    public void ReadLimit_IsBoundedByRequestedLimit()
    {
        var trail = new InMemoryOperatorAuditTrail(10);
        var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < 5; index++) trail.Record(Event(index, start.AddSeconds(index)));

        var events = trail.GetRecent(2);

        Assert.Equal(2, events.Count);
        Assert.Equal("incident-4", events[0].ResourceId);
        Assert.Equal("incident-3", events[1].ResourceId);
    }

    [Fact]
    public void AuditShape_DoesNotContainSensitivePayloadFields()
    {
        var serialized = JsonSerializer.Serialize(Event(1, DateTimeOffset.UtcNow));

        Assert.DoesNotContain("Evidence", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sql", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Endpoint", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Provider", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidUnboundedActor_IsRejected()
    {
        var trail = new InMemoryOperatorAuditTrail();
        var auditEvent = Event(1, DateTimeOffset.UtcNow) with { Actor = new string('a', 129) };

        Assert.Throws<ArgumentException>(() => trail.Record(auditEvent));
        Assert.Empty(trail.GetRecent());
    }

    private static OperatorAuditEvent Event(int index, DateTimeOffset occurredAtUtc) => new(
        Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}"),
        occurredAtUtc,
        "DOMAIN\\operator",
        OperatorAuditAction.IncidentResolved,
        "Incident",
        $"incident-{index}",
        "Acknowledged",
        "Resolved");
}
