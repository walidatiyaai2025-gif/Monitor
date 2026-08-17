using System.Text;
using Microsoft.AspNetCore.Authorization;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800FleetDecisionSupportExportTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Export_UnavailableEvidenceIsExplicitAndDoesNotInventZeroSummaries()
    {
        var snapshot = EmptySnapshot() with
        {
            IncidentEvidenceComplete = false,
            ServerPolicyEvidenceComplete = false,
            IncidentPolicyEvidenceComplete = false,
            OperatorPolicyUnavailable = 3,
            IncidentRisk = null,
            DecisionSupport = null
        };

        var csv = Text(FleetDecisionSupportExport.Build(snapshot));

        Assert.Contains("#schema,monitor-export-v2", csv, StringComparison.Ordinal);
        Assert.Contains("\"Evidence\",\"IncidentEvidenceComplete\",\"false\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Evidence\",\"ServerPolicyEvidenceComplete\",\"false\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Evidence\",\"IncidentPolicyEvidenceComplete\",\"false\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Evidence\",\"OperatorPolicyUnavailable\",\"3\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"FleetRisk\",\"State\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"DecisionSupport\",\"State\",\"Unavailable\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Routing\",\"EvaluatedIncidents\",\"0\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Correlation\",\"TotalClusters\",\"0\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_UsesAggregateRoutingAndSafeCorrelationDetailWithoutSensitiveRoutingIdentifiers()
    {
        var sensitiveRouting = new FleetRoutingSuggestion(
            "INCIDENT-SECRET-123",
            "SERVER-SECRET-456",
            "rule.secret",
            "Production",
            FindingSeverity.Critical,
            AlertRoute.Page,
            3,
            TimeSpan.FromMinutes(5),
            "ASSIGNEE-SECRET",
            "REASON-SECRET",
            "DEDUP-SECRET",
            false,
            false);
        var cluster = new SignalCluster(
            "=cluster-key",
            new DateTimeOffset(2026, 8, 17, 18, 30, 0, TimeSpan.Zero),
            "+dominant-rule",
            2,
            ["@production", "DR"],
            B400Severity.Critical,
            88.25);
        var decision = new FleetDecisionSupportSnapshot(
            TimeSpan.FromMinutes(5),
            1,
            [cluster],
            [sensitiveRouting],
            new FleetRoutingSummary(1, 1, 0, 0, 0, 0, 0, 1),
            new FleetCorrelationSummary(1, 1, 1, 0, 0, 1, 2, 88.25));
        var snapshot = EmptySnapshot() with
        {
            IncidentRisk = new Batch300FleetRiskSummary(92, FleetRiskLevel.Critical, 1, 0, ["safe.rule"]),
            DecisionSupport = decision
        };

        var csv = Text(FleetDecisionSupportExport.Build(snapshot));

        Assert.Contains("\"FleetRisk\",\"Score\",\"92\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Routing\",\"EvaluatedIncidents\",\"1\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Correlation\",\"TotalClusters\",\"1\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"CorrelationDetail\",\"Cluster\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"'=cluster-key\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"'+dominant-rule\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"'@production|DR\"", csv, StringComparison.Ordinal);

        Assert.DoesNotContain("INCIDENT-SECRET-123", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("SERVER-SECRET-456", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("ASSIGNEE-SECRET", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("REASON-SECRET", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("DEDUP-SECRET", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_CorrelationDetailIsBoundedToExistingTopTwentyContract()
    {
        var clusters = Enumerable.Range(1, 25)
            .Select(index => new SignalCluster(
                $"CLUSTER-{index:00}",
                new DateTimeOffset(2026, 8, 17, 18, index % 60, 0, TimeSpan.Zero),
                $"RULE-{index:00}",
                1,
                ["PRODUCTION"],
                B400Severity.Warning,
                60))
            .ToArray();
        var decision = new FleetDecisionSupportSnapshot(
            TimeSpan.FromMinutes(5),
            25,
            clusters,
            [],
            new FleetRoutingSummary(25, 0, 0, 25, 0, 0, 0, 25),
            new FleetCorrelationSummary(25, 25, 0, 25, 0, 0, 1, 60));

        var csv = Text(FleetDecisionSupportExport.Build(EmptySnapshot() with { DecisionSupport = decision }));

        Assert.Equal(FleetDecisionSupport.MaxItems, Count(csv, "\"CorrelationDetail\",\"Cluster\""));
        Assert.Contains("CLUSTER-20", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("CLUSTER-21", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Route_IsViewerSafeUsesCentralDownloadPolicyAndIsDiscoverable()
    {
        var controller = typeof(EnterpriseReportsController);
        var classPolicy = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy;
        var action = controller.GetMethod(nameof(EnterpriseReportsController.FleetDecisionSupport))!;
        var actionPolicies = action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray();

        Assert.Equal(MonitorPolicies.Read, classPolicy);
        Assert.Empty(actionPolicies);
        Assert.Equal(
            "monitor-fleetdecisionsupport-20260817-183000.csv",
            EnterpriseSecurityPolicy.SafeDownloadFileName(
                EnterpriseDownloadSubject.FleetDecisionSupport,
                new DateTimeOffset(2026, 8, 17, 18, 30, 0, TimeSpan.Zero),
                "csv"));

        var source = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        var view = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");
        Assert.Contains("/reports/fleet-decision-support.csv", source, StringComparison.Ordinal);
        Assert.Contains("EnterpriseDownloadSubject.FleetDecisionSupport", source, StringComparison.Ordinal);
        Assert.Contains("/reports/fleet-decision-support.csv", view, StringComparison.Ordinal);
        Assert.Contains("Incident/server IDs and assignee names are excluded", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", source, StringComparison.Ordinal);
    }

    private static FleetIntelligenceSnapshot EmptySnapshot() => new(
        [],
        [],
        [],
        0,
        0,
        0,
        0,
        0,
        [],
        new FleetRiskSummary(0, 0, 0, 0));

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static int Count(string value, string fragment)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }
        return count;
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
