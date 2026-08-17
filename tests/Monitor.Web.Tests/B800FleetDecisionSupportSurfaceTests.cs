using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800FleetDecisionSupportSurfaceTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void FleetService_UsesBoundedIncidentsExplicitOperatorPolicyAvailabilityAndExistingFleetRiskContract()
    {
        var service = Read("src/Monitor.Web/Services/FleetIntelligenceService.cs");

        Assert.Contains("BoundedIncidentReadModel.ActiveForRegistrations", service, StringComparison.Ordinal);
        Assert.Contains("OperatorPolicyReadService", service, StringComparison.Ordinal);
        Assert.Contains("operatorPolicy.GetServers", service, StringComparison.Ordinal);
        Assert.Contains("operatorPolicy.GetIncidents", service, StringComparison.Ordinal);
        Assert.Contains("PolicyReadable", service, StringComparison.Ordinal);
        Assert.Contains("incidentRead.IsComplete && incidentPolicyEvidenceComplete", service, StringComparison.Ordinal);
        Assert.Contains("item.Server!.Policy.Environment", service, StringComparison.Ordinal);
        Assert.Contains("FleetDecisionSupport.Build", service, StringComparison.Ordinal);
        Assert.Contains("Batch300FleetRisk.Summarize", service, StringComparison.Ordinal);
        Assert.Contains("Batch400FleetCorrelation.SeverityWeight", service, StringComparison.Ordinal);
        Assert.Contains("IncidentRisk", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadAssignee", service, StringComparison.Ordinal);
        Assert.DoesNotContain("operatorMetadata.GetServer", service, StringComparison.Ordinal);
        Assert.DoesNotContain("operatorMetadata.GetIncident", service, StringComparison.Ordinal);
        Assert.DoesNotContain("incidents.GetAll()", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SnapshotQuery", service, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionSupportSurface_IsReadOnlyExplicitlyNonExecutingAndTruthfulOnIncompleteEvidence()
    {
        var view = Read("src/Monitor.Web/Views/Shared/_FleetDecisionSupport.cshtml");
        var fleet = Read("src/Monitor.Web/Views/FleetIntelligence/Index.cshtml");

        Assert.Contains("CORRELATION · DECISION SUPPORT", view, StringComparison.Ordinal);
        Assert.Contains("Correlation coverage above evaluates all", view, StringComparison.Ordinal);
        Assert.Contains("complete bounded Fleet decision population", view, StringComparison.Ordinal);
        Assert.Contains("top-@FleetDecisionSupport.MaxItems cluster detail view only", view, StringComparison.Ordinal);
        Assert.Contains("Full correlation coverage is not evaluated", view, StringComparison.Ordinal);
        Assert.Contains("B400 correlation coverage bound", view, StringComparison.Ordinal);
        Assert.Contains("Multi-server", view, StringComparison.Ordinal);
        Assert.Contains("Highest score", view, StringComparison.Ordinal);
        Assert.Contains("ROUTING · RECOMMENDATION ONLY", view, StringComparison.Ordinal);
        Assert.Contains("No notification is sent", view, StringComparison.Ordinal);
        Assert.Contains("no sender, pager or mutation action", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Routing coverage above evaluates all", view, StringComparison.Ordinal);
        Assert.Contains("top-@FleetDecisionSupport.MaxItems detail view only", view, StringComparison.Ordinal);
        Assert.Contains("Evaluated", view, StringComparison.Ordinal);
        Assert.Contains("Unassigned", view, StringComparison.Ordinal);
        Assert.Contains("_FleetDecisionSupport", fleet, StringComparison.Ordinal);
        Assert.Contains("Fleet decision support not evaluated", fleet, StringComparison.Ordinal);
        Assert.Contains("partial incident set", fleet, StringComparison.Ordinal);
        Assert.Contains("Some fleet policy facts are unavailable", fleet, StringComparison.Ordinal);
        Assert.Contains("maintenance and suppression totals are withheld", fleet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Operator metadata required for one or more active incidents is unavailable", fleet, StringComparison.Ordinal);
        Assert.Contains("an unavailable metadata read is a different state", fleet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rule hot-spots are unavailable", fleet, StringComparison.Ordinal);
        Assert.Contains("required operator policy metadata could not be read", fleet, StringComparison.Ordinal);
        Assert.Contains("Bounded active-incident risk", fleet, StringComparison.Ordinal);
        Assert.Contains("READ-ONLY SCORE", fleet, StringComparison.Ordinal);
        Assert.Contains("Batch300FleetRisk", Read("src/Monitor.Web/Services/FleetIntelligenceService.cs"), StringComparison.Ordinal);
        Assert.Contains("Incident risk, correlation clusters", fleet, StringComparison.Ordinal);
        Assert.Contains("decision support only and performs no notification, mutation or remediation", fleet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("method=\"post\"", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-action=\"Send", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-action=\"Page", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-action=\"Notify", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecisionSupport_DelegatesCorrelationRoutingAndIncidentRiskToExistingB300B400Contracts()
    {
        var contract = Read("src/Monitor.Web/Services/FleetDecisionSupport.cs");
        var correlation = Read("src/Monitor.Web/Services/Batch400FleetCorrelation.cs");
        var fleet = Read("src/Monitor.Web/Services/FleetIntelligenceService.cs");

        Assert.Contains("Batch400FleetCorrelation.Correlate", contract, StringComparison.Ordinal);
        Assert.Contains("Batch400FleetCorrelation.ClampWindow(TimeSpan.Zero)", contract, StringComparison.Ordinal);
        Assert.Contains("Batch400FleetCorrelation.SeverityWeight", contract, StringComparison.Ordinal);
        Assert.Contains("Batch400FleetCorrelation.MaxClusterLimit", contract, StringComparison.Ordinal);
        Assert.Contains("FleetCorrelationSummary", contract, StringComparison.Ordinal);
        Assert.Contains("allCorrelations.Take(MaxItems)", contract, StringComparison.Ordinal);
        Assert.Contains("public const int MaxClusterLimit = 100", correlation, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(limit, 1, MaxClusterLimit)", correlation, StringComparison.Ordinal);
        Assert.Contains("Batch300AlertRouting.Decide", contract, StringComparison.Ordinal);
        Assert.Contains("FleetRoutingSummary", contract, StringComparison.Ordinal);
        Assert.Contains("routingDecisions.Length", contract, StringComparison.Ordinal);
        Assert.Contains("routingDecisions.Take(MaxItems)", contract, StringComparison.Ordinal);
        Assert.Contains("SuggestedRoute", contract, StringComparison.Ordinal);
        Assert.Contains("Batch300FleetRisk.Summarize", fleet, StringComparison.Ordinal);
        Assert.Contains("timeProvider.GetUtcNow()", fleet, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("Smtp", contract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Webhook", contract, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
