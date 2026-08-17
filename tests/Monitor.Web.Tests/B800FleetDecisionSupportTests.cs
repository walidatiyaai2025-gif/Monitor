using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800FleetDecisionSupportTests
{
    [Fact]
    public void Build_CorrelatesSameRuleEnvironmentWindowAcrossServers()
    {
        var at = new DateTimeOffset(2026, 8, 17, 10, 2, 0, TimeSpan.Zero);
        var snapshot = FleetDecisionSupport.Build(
        [
            I("INC-1", "11111111-1111-1111-1111-111111111111", "BLOCKING", FindingSeverity.Critical, at, "Production"),
            I("INC-2", "22222222-2222-2222-2222-222222222222", "BLOCKING", FindingSeverity.Warning, at.AddMinutes(1), "Production")
        ]);

        var cluster = Assert.Single(snapshot.Correlations);
        Assert.Equal(TimeSpan.FromMinutes(5), snapshot.CorrelationWindow);
        Assert.Equal("BLOCKING", cluster.DominantRule);
        Assert.Equal(2, cluster.AffectedServers);
        Assert.Equal(B400Severity.Critical, cluster.Severity);
        Assert.Contains("PRODUCTION", cluster.Environments);
    }

    [Fact]
    public void Build_SuggestsRoutingWithoutExecutingAndHonorsSuppressionAndMaintenance()
    {
        var at = DateTimeOffset.UtcNow;
        var snapshot = FleetDecisionSupport.Build(
        [
            I("INC-PAGE", "11111111-1111-1111-1111-111111111111", "MEMORY", FindingSeverity.Critical, at, "Production", assignee: "dba-oncall"),
            I("INC-SUP", "22222222-2222-2222-2222-222222222222", "MEMORY", FindingSeverity.Critical, at, "Production", suppressed: true),
            I("INC-MAINT", "33333333-3333-3333-3333-333333333333", "BLOCKING", FindingSeverity.Critical, at, "Production", maintenance: true)
        ]);

        var page = snapshot.RoutingSuggestions.Single(item => item.IncidentId == "INC-PAGE");
        var suppressed = snapshot.RoutingSuggestions.Single(item => item.IncidentId == "INC-SUP");
        var maintenance = snapshot.RoutingSuggestions.Single(item => item.IncidentId == "INC-MAINT");

        Assert.Equal(AlertRoute.Page, page.SuggestedRoute);
        Assert.Equal(3, page.EscalationTier);
        Assert.Equal("dba-oncall", page.Owner);
        Assert.Equal(AlertRoute.None, suppressed.SuggestedRoute);
        Assert.Equal("suppressed", suppressed.Reason);
        Assert.Equal(AlertRoute.None, maintenance.SuggestedRoute);
        Assert.Equal("maintenance", maintenance.Reason);

        var summary = Assert.IsType<FleetRoutingSummary>(snapshot.RoutingSummary);
        Assert.Equal(3, summary.EvaluatedIncidents);
        Assert.Equal(1, summary.Page);
        Assert.Equal(0, summary.Notify);
        Assert.Equal(0, summary.Queue);
        Assert.Equal(2, summary.None);
        Assert.Equal(1, summary.Suppressed);
        Assert.Equal(1, summary.InMaintenance);
        Assert.Equal(2, summary.Unassigned);
    }

    [Fact]
    public void Build_RoutingSummaryCoversFullBoundedPopulationWhileDetailRemainsTopTwenty()
    {
        var at = DateTimeOffset.UtcNow;
        var items = Enumerable.Range(1, 35)
            .Select(index => I(
                $"INC-{index:00}",
                $"00000000-0000-0000-0000-{index:000000000000}",
                $"RULE-{index % 3}",
                index % 2 == 0 ? FindingSeverity.Critical : FindingSeverity.Warning,
                at.AddSeconds(index),
                index % 5 == 0 ? "Production" : "Staging",
                suppressed: index % 11 == 0,
                maintenance: index % 13 == 0,
                assignee: index % 4 == 0 ? "dba-team" : null))
            .ToArray();

        var snapshot = FleetDecisionSupport.Build(items);

        var summary = Assert.IsType<FleetRoutingSummary>(snapshot.RoutingSummary);
        Assert.Equal(35, snapshot.InputIncidents);
        Assert.Equal(35, summary.EvaluatedIncidents);
        Assert.Equal(35, summary.Page + summary.Notify + summary.Queue + summary.None);
        Assert.Equal(items.Count(item => item.Suppressed), summary.Suppressed);
        Assert.Equal(items.Count(item => item.InMaintenance), summary.InMaintenance);
        Assert.Equal(items.Count(item => string.IsNullOrWhiteSpace(item.Assignee)), summary.Unassigned);
        Assert.Equal(FleetDecisionSupport.MaxItems, snapshot.RoutingSuggestions.Count);
        Assert.True(snapshot.Correlations.Count <= FleetDecisionSupport.MaxItems);
    }

    [Fact]
    public void Build_IsBoundedAndUsesOpaqueDedupKeys()
    {
        var at = DateTimeOffset.UtcNow;
        var items = Enumerable.Range(1, 35)
            .Select(index => I(
                $"INC-{index:00}",
                $"00000000-0000-0000-0000-{index:000000000000}",
                $"RULE-{index % 3}",
                index % 2 == 0 ? FindingSeverity.Critical : FindingSeverity.Warning,
                at.AddSeconds(index),
                "Staging"))
            .ToArray();

        var snapshot = FleetDecisionSupport.Build(items);

        Assert.Equal(35, snapshot.InputIncidents);
        Assert.Equal(35, Assert.IsType<FleetRoutingSummary>(snapshot.RoutingSummary).EvaluatedIncidents);
        Assert.True(snapshot.RoutingSuggestions.Count <= FleetDecisionSupport.MaxItems);
        Assert.True(snapshot.Correlations.Count <= FleetDecisionSupport.MaxItems);
        Assert.All(snapshot.RoutingSuggestions, item =>
        {
            Assert.Equal(20, item.DedupKey.Length);
            Assert.DoesNotContain(item.RuleId, item.DedupKey, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Build_DropsInvalidIncidentIdentityBeforeDecisionSupportAndRoutingSummary()
    {
        var valid = I("INC-1", "11111111-1111-1111-1111-111111111111", "RULE", FindingSeverity.Warning, DateTimeOffset.UtcNow, "Test");
        var snapshot = FleetDecisionSupport.Build(
        [
            valid,
            valid with { IncidentId = " " },
            valid with { RegistrationId = Guid.Empty },
            valid with { RuleId = "" }
        ]);

        Assert.Equal(1, snapshot.InputIncidents);
        Assert.Equal(1, Assert.IsType<FleetRoutingSummary>(snapshot.RoutingSummary).EvaluatedIncidents);
        Assert.Single(snapshot.RoutingSuggestions);
        Assert.Single(snapshot.Correlations);
    }

    private static FleetDecisionIncident I(
        string incidentId,
        string registrationId,
        string ruleId,
        FindingSeverity severity,
        DateTimeOffset at,
        string environment,
        bool suppressed = false,
        bool maintenance = false,
        string? assignee = null) =>
        new(incidentId, Guid.Parse(registrationId), ruleId, severity, at, environment, suppressed, maintenance, assignee);
}
