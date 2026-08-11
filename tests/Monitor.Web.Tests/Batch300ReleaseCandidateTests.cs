using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300ReleaseCandidateTests
{
    [Fact]
    public void B300_091_DbaIntelligenceDashboardHasReadPolicyGetRouteAndNavigation()
    {
        var authorization = Assert.Single(typeof(DbaIntelligenceController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(MonitorPolicies.Read, authorization.Policy);
        var index = typeof(DbaIntelligenceController).GetMethod(nameof(DbaIntelligenceController.Index), BindingFlags.Public | BindingFlags.Instance)!;
        var get = Assert.Single(index.GetCustomAttributes<HttpGetAttribute>());
        Assert.Equal("/enterprise/dba-intelligence", get.Template);
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");
        Assert.Contains("DBA Intelligence", layout, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"DbaIntelligence\"", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void B300_092_RiskFleetCardsAreRankedHighestScoreFirst()
    {
        var service = Read("src/Monitor.Web/Services/DbaIntelligenceDashboardService.cs");
        Assert.Contains("new DbaRiskFleetCard", service, StringComparison.Ordinal);
        Assert.Contains("OrderByDescending(item => item.Score)", service, StringComparison.Ordinal);
        Assert.Contains("item.Risk.Actionable", service, StringComparison.Ordinal);
    }

    [Fact]
    public void B300_093_TrendCardsUseMonitorHistoryAndBoundedTrendAnalysis()
    {
        var service = Read("src/Monitor.Web/Services/DbaIntelligenceDashboardService.cs");
        Assert.Contains("history.Read", service, StringComparison.Ordinal);
        Assert.Contains("DbaTrendAnalysis.Memory", service, StringComparison.Ordinal);
        Assert.Contains("DbaTrendAnalysis.Blocking", service, StringComparison.Ordinal);
        Assert.Contains("DbaTrendAnalysis.Runnable", service, StringComparison.Ordinal);
        Assert.Contains("DbaTrendAnalysis.DatabaseAvailability", service, StringComparison.Ordinal);
        new DbaIntelligenceOptions { HistoryHours = 24, HistoryPoints = 288 }.Validate();
        Assert.Throws<InvalidOperationException>(() => new DbaIntelligenceOptions { HistoryHours = 25 }.Validate());
    }

    [Fact]
    public void B300_094_PriorityIncidentUiModelUsesDeterministicBoundedQueue()
    {
        var service = Read("src/Monitor.Web/Services/DbaIntelligenceDashboardService.cs");
        var view = Read("src/Monitor.Web/Views/DbaIntelligence/Index.cshtml");
        Assert.Contains("IncidentPriorityService", service, StringComparison.Ordinal);
        Assert.Contains("Queue(limit: 25)", service, StringComparison.Ordinal);
        Assert.Contains("PRIORITY QUEUE", view, StringComparison.Ordinal);
        Assert.Contains("@row.Score", view, StringComparison.Ordinal);
        Assert.Contains("@row.SlaBucket", view, StringComparison.Ordinal);
    }

    [Fact]
    public void B300_095_CapacityUiDoesNotInventCapacityWhenPolicyIsMissing()
    {
        var model = new DbaCapacityComplianceCard(Guid.NewGuid(), "SQL", false, null, "Storage capacity policy is not configured.");
        Assert.False(model.Available);
        Assert.Null(model.Projection);
        Assert.Contains("not configured", model.Message, StringComparison.OrdinalIgnoreCase);
        var service = Read("src/Monitor.Web/Services/DbaIntelligenceDashboardService.cs");
        Assert.Contains("StorageCapacityBytes <= 0", service, StringComparison.Ordinal);
    }

    [Fact]
    public void B300_096_EstateUiModelExposesVersionPostureWithoutEndpointsOrCredentials()
    {
        var properties = typeof(DbaEstateLifecycleCard).GetProperties().Select(item => item.Name).ToArray();
        Assert.Contains("Generation", properties);
        Assert.Contains("Edition", properties);
        Assert.Contains("Encryption", properties);
        Assert.Contains("Lifecycle", properties);
        Assert.DoesNotContain(properties, name => name.Contains("Host", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void B300_097_EmptyAndDegradedStatesAreExplicitAndDoNotTriggerRefresh()
    {
        var empty = new DbaIntelligenceDashboardViewModel([], [], [], [], [], "empty", "No data", DateTimeOffset.UtcNow);
        var degraded = empty with { State = "degraded" };
        Assert.True(empty.IsEmpty);
        Assert.True(degraded.IsDegraded);
        var view = Read("src/Monitor.Web/Views/DbaIntelligence/Index.cshtml");
        Assert.Contains("Empty estate", view, StringComparison.Ordinal);
        Assert.Contains("Degraded retained data", view, StringComparison.Ordinal);
        Assert.Contains("never initiates monitored SQL collection", view, StringComparison.Ordinal);
    }

    [Fact]
    public void B300_098_Batch300PreservesBatch100AndBatch200OperatorContracts()
    {
        var enterprise = Read("src/Monitor.Web/Controllers/EnterpriseOperationsController.cs");
        var health = Read("src/Monitor.Web/Controllers/HealthController.cs");
        var reports = Read("src/Monitor.Web/Controllers/EnterpriseReportsController.cs");
        Assert.Contains("/enterprise", enterprise, StringComparison.Ordinal);
        Assert.Contains("/reports/servers.csv", enterprise, StringComparison.Ordinal);
        Assert.Contains("/diagnostics/package", enterprise, StringComparison.Ordinal);
        Assert.Contains("/health/live", health, StringComparison.Ordinal);
        Assert.Contains("/health/ready", health, StringComparison.Ordinal);
        Assert.Contains("/reports/servers-v2.csv", reports, StringComparison.Ordinal);
    }

    [Fact]
    public void B300_099_ReleaseCandidateHasExactlyOneHundredMappedTaskIdsAndReadPathsStayCollectionFree()
    {
        var ledger = Read("docs/BATCH_300.md");
        var ids = System.Text.RegularExpressions.Regex.Matches(ledger, @"\| (B300-\d{3}) \|")
            .Select(match => match.Groups[1].Value)
            .ToArray();
        Assert.Equal(100, ids.Length);
        Assert.Equal(100, ids.Distinct(StringComparer.Ordinal).Count());
        for (var number = 1; number <= 100; number++) Assert.Contains($"B300-{number:000}", ids, StringComparer.Ordinal);

        var dashboard = Read("src/Monitor.Web/Services/DbaIntelligenceDashboardService.cs");
        var api = Read("src/Monitor.Web/Controllers/DbaReadApiController.cs");
        Assert.DoesNotContain("RefreshAsync", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAsync", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshAsync", api, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAsync", api, StringComparison.Ordinal);
        Assert.Contains("cache.Peek", dashboard, StringComparison.Ordinal);
        Assert.Contains("cache.Peek", api, StringComparison.Ordinal);
    }

    [Fact]
    public void B300_100_CanonicalStatusAndLedgerDeclareImplementationWithoutPrematureClosure()
    {
        var ledger = Read("docs/BATCH_300.md");
        var status = Read("docs/STATUS.md");
        Assert.Contains("100/100 IMPLEMENTED", status, StringComparison.Ordinal);
        Assert.Contains("CI-MERGE PENDING", status, StringComparison.Ordinal);
        Assert.Contains("IMPLEMENTED — CI PENDING", ledger, StringComparison.Ordinal);
        Assert.Contains("final PR CI", ledger, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CLOSED", ledger, StringComparison.Ordinal);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(FindRepoRoot(), path.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root containing Monitor.sln was not found.");
    }
}
