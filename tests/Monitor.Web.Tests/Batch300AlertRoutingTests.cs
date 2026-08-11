using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300AlertRoutingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);
    private static AlertRoutingInput Input(int severity = 95, string environment = "production") => new("memory-high", environment, severity, false, false, null, Now);

    [Fact] public void B300_061_NormalizeEnvironment_MapsAliases() => Assert.Equal("production", Batch300AlertRouting.NormalizeEnvironment("PROD"));

    [Fact] public void B300_062_EscalationTier_PrioritizesCriticalProduction() => Assert.Equal(3, Batch300AlertRouting.EscalationTier(95, "production"));

    [Fact] public void B300_063_Route_SuppressionWins() => Assert.Equal(AlertRoute.None, Batch300AlertRouting.Route(Input() with { Suppressed = true }));

    [Fact] public void B300_064_ShouldPage_OnlyForPageRoute() => Assert.True(Batch300AlertRouting.ShouldPage(Input()));

    [Fact] public void B300_065_Cooldown_ShortensForHigherTier()
    {
        Assert.True(Batch300AlertRouting.Cooldown(3) < Batch300AlertRouting.Cooldown(1));
    }

    [Fact] public void B300_066_Owner_FallsBackToUnassigned() => Assert.Equal("unassigned", Batch300AlertRouting.Owner(" "));

    [Fact] public void B300_067_DedupKey_IsStableAndOpaque()
    {
        var first = Batch300AlertRouting.DedupKey("memory-high", "prod");
        var second = Batch300AlertRouting.DedupKey("memory-high", "production");
        Assert.Equal(first, second);
        Assert.Equal(20, first.Length);
    }

    [Fact] public void B300_068_Reason_ExplainsMaintenance() => Assert.Equal("maintenance", Batch300AlertRouting.Reason(Input() with { InMaintenance = true }));

    [Fact] public void B300_069_InQuietWindow_HandlesOvernightWindow()
    {
        Assert.True(Batch300AlertRouting.InQuietWindow(new TimeOnly(23, 0), new TimeOnly(22, 0), new TimeOnly(6, 0)));
        Assert.True(Batch300AlertRouting.InQuietWindow(new TimeOnly(2, 0), new TimeOnly(22, 0), new TimeOnly(6, 0)));
    }

    [Fact] public void B300_070_Decide_CombinesRouteTierCooldownAndOwner()
    {
        var decision = Batch300AlertRouting.Decide(Input() with { Assignee = " DBA Team " });
        Assert.Equal(AlertRoute.Page, decision.Route);
        Assert.Equal(3, decision.EscalationTier);
        Assert.Equal("DBA Team", decision.Owner);
    }
}
