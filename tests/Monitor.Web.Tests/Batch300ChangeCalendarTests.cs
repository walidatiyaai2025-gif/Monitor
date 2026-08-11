using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300ChangeCalendarTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");
    private static readonly Guid ServerId = Guid.Parse("12345678-1234-1234-1234-123456789012");

    [Fact]
    public void B300_041_ChangeWindowModelPreservesScopeAndFreezeIntent()
    {
        var item = Window(ChangeScopeKind.Server, ServerId.ToString("D"), freeze: true);
        Assert.Equal(ChangeScopeKind.Server, item.ScopeKind);
        Assert.True(item.Freeze);
        Assert.Equal("planned change", item.Reason);
    }

    [Fact]
    public void B300_042_ValidationRequiresUtcPositiveBoundedDuration()
    {
        ChangeCalendarValidation.Normalize(Window(ChangeScopeKind.Server, ServerId.ToString("D")));
        Assert.Throws<ArgumentException>(() => ChangeCalendarValidation.Normalize(Window(ChangeScopeKind.Server, ServerId.ToString("D")) with { EndUtc = Now.AddDays(32) }));
        Assert.Throws<ArgumentException>(() => ChangeCalendarValidation.Normalize(Window(ChangeScopeKind.Server, ServerId.ToString("D")) with { EndUtc = Now.AddMinutes(-1) }));
        Assert.Throws<ArgumentException>(() => ChangeCalendarValidation.Normalize(Window(ChangeScopeKind.Server, ServerId.ToString("D")) with { StartUtc = Now.ToOffset(TimeSpan.FromHours(3)) }));
    }

    [Fact]
    public void B300_043_GroupWindowMatchesOnlyServerInSameGroup()
    {
        var f = Fixture();
        Assert.True(f.Service.Add(Window(ChangeScopeKind.Group, "core")));
        Assert.Single(f.Service.ForServer(ServerId, Metadata(group: "core")));
        Assert.Empty(f.Service.ForServer(ServerId, Metadata(group: "edge")));
    }

    [Fact]
    public void B300_044_EnvironmentWindowMatchesEnvironmentClassification()
    {
        var f = Fixture();
        Assert.True(f.Service.Add(Window(ChangeScopeKind.Environment, "Production")));
        Assert.Single(f.Service.ForServer(ServerId, Metadata(environment: ServerEnvironmentClass.Production)));
        Assert.Empty(f.Service.ForServer(ServerId, Metadata(environment: ServerEnvironmentClass.Test)));
    }

    [Fact]
    public void B300_045_OverlapDetectionUsesSameScopeAndHalfOpenIntervals()
    {
        var first = Window(ChangeScopeKind.Group, "core") with { StartUtc = Now, EndUtc = Now.AddHours(1) };
        var overlap = first with { Id = Guid.NewGuid(), StartUtc = Now.AddMinutes(30), EndUtc = Now.AddHours(2) };
        var adjacent = first with { Id = Guid.NewGuid(), StartUtc = Now.AddHours(1), EndUtc = Now.AddHours(2) };
        Assert.True(ChangeCalendarValidation.Overlaps(first, overlap));
        Assert.False(ChangeCalendarValidation.Overlaps(first, adjacent));
    }

    [Fact]
    public void B300_046_UpcomingProjectionIsChronologicalAndHorizonBounded()
    {
        var f = Fixture();
        Assert.True(f.Service.Add(Window(ChangeScopeKind.Server, ServerId.ToString("D")) with { StartUtc = Now.AddHours(5), EndUtc = Now.AddHours(6) }));
        Assert.True(f.Service.Add(Window(ChangeScopeKind.Group, "core") with { StartUtc = Now.AddHours(2), EndUtc = Now.AddHours(3) }));
        var upcoming = f.Service.Upcoming(TimeSpan.FromHours(4));
        var row = Assert.Single(upcoming);
        Assert.Equal(ChangeScopeKind.Group, row.ScopeKind);
    }

    [Fact]
    public void B300_047_ActiveProjectionIsStartInclusiveEndExclusive()
    {
        var f = Fixture();
        var item = Window(ChangeScopeKind.Server, ServerId.ToString("D")) with { StartUtc = Now, EndUtc = Now.AddHours(1) };
        Assert.True(f.Service.Add(item));
        Assert.Single(f.Service.At(Now));
        Assert.Empty(f.Service.At(Now.AddHours(1)));
    }

    [Fact]
    public void B300_048_ChangeFreezePolicyMatchesServerGroupAndEnvironment()
    {
        var f = Fixture();
        Assert.True(f.Service.Add(Window(ChangeScopeKind.Group, "core", freeze: true)));
        Assert.True(f.Service.IsFrozen(ServerId, Metadata(group: "core")));
        Assert.False(f.Service.IsFrozen(ServerId, Metadata(group: "edge")));
    }

    [Fact]
    public void B300_049_MutationsCreateAuditableOutcomeWithoutReasonPayload()
    {
        var f = Fixture();
        var item = Window(ChangeScopeKind.Server, ServerId.ToString("D"));
        Assert.True(f.Service.Add(item));
        Assert.True(f.Service.Remove(item.Id, "admin"));
        Assert.Contains(f.Audit.Items, row => row.Action == "change-window.add" && row.Outcome == "applied");
        Assert.Contains(f.Audit.Items, row => row.Action == "change-window.remove" && row.Outcome == "applied");
        Assert.DoesNotContain(f.Audit.Items, row => row.Target.Contains(item.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void B300_050_DuplicateOrOverlappingWindowIsRejectedAndAudited()
    {
        var f = Fixture();
        var first = Window(ChangeScopeKind.Group, "core");
        var second = first with { Id = Guid.NewGuid(), StartUtc = first.StartUtc.AddMinutes(5), EndUtc = first.EndUtc.AddMinutes(5) };
        Assert.True(f.Service.Add(first));
        Assert.False(f.Service.Add(second));
        Assert.Equal(2, f.Audit.Items.Count(row => row.Action == "change-window.add"));
        Assert.Contains(f.Audit.Items, row => row.Outcome == "rejected:overlap-or-duplicate");
    }

    private static ChangeWindow Window(ChangeScopeKind kind, string value, bool freeze = false) =>
        new(Guid.NewGuid(), kind, value, Now.AddMinutes(-10), Now.AddMinutes(10), "planned change", freeze, Now, "admin");

    private static ServerOperatorMetadata Metadata(string? group = "core", ServerEnvironmentClass environment = ServerEnvironmentClass.Production) =>
        new(ServerId, environment, group, [], null, null, Now);

    private static FixtureState Fixture()
    {
        var audit = new TestAuditStore();
        var clock = new FixedTimeProvider(Now);
        return new(new ChangeCalendarService(new InMemoryChangeCalendarStore(), audit, clock), audit);
    }

    private sealed record FixtureState(ChangeCalendarService Service, TestAuditStore Audit);
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class TestAuditStore : IAuditStore
    {
        public List<AuditEvent> Items { get; } = [];
        public void Append(string actor, string action, string target, string outcome) => Items.Add(new(Guid.NewGuid(), Now, actor, action, target, outcome));
        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => Items.Skip(offset).Take(limit).ToArray();
    }
}
