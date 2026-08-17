using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800FleetDecisionSupportSurfaceTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void FleetService_UsesBoundedActiveIncidentsAndOperatorPolicyFactsOnly()
    {
        var service = Read("src/Monitor.Web/Services/FleetIntelligenceService.cs");

        Assert.Contains("BoundedIncidentReadModel.ActiveForRegistrations", service, StringComparison.Ordinal);
        Assert.Contains("incidentRead.IsComplete", service, StringComparison.Ordinal);
        Assert.Contains("item.Server.Suppressed", service, StringComparison.Ordinal);
        Assert.Contains("item.Server.Maintenance", service, StringComparison.Ordinal);
        Assert.Contains("ReadAssignee(item.Incident.Id)", service, StringComparison.Ordinal);
        Assert.Contains("FleetDecisionSupport.Build", service, StringComparison.Ordinal);
        Assert.DoesNotContain("incidents.GetAll()", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SnapshotQuery", service, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionSupportSurface_IsReadOnlyExplicitlyNonExecutingAndTruthfulOnOverflow()
    {
        var view = Read("src/Monitor.Web/Views/Shared/_FleetDecisionSupport.cshtml");
        var fleet = Read("src/Monitor.Web/Views/FleetIntelligence/Index.cshtml");

        Assert.Contains("ROUTING · RECOMMENDATION ONLY", view, StringComparison.Ordinal);
        Assert.Contains("No notification is sent", view, StringComparison.Ordinal);
        Assert.Contains("no sender, pager or mutation action", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("_FleetDecisionSupport", fleet, StringComparison.Ordinal);
        Assert.Contains("Fleet decision support not evaluated", fleet, StringComparison.Ordinal);
        Assert.Contains("partial incident set", fleet, StringComparison.Ordinal);
        Assert.Contains("Rule hot-spots are unavailable", fleet, StringComparison.Ordinal);
        Assert.DoesNotContain("method=\"post\"", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-action=\"Send", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-action=\"Page", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-action=\"Notify", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecisionSupport_DelegatesCorrelationAndRoutingToExistingB300B400Contracts()
    {
        var contract = Read("src/Monitor.Web/Services/FleetDecisionSupport.cs");

        Assert.Contains("Batch400FleetCorrelation.Correlate", contract, StringComparison.Ordinal);
        Assert.Contains("Batch400FleetCorrelation.ClampWindow(TimeSpan.Zero)", contract, StringComparison.Ordinal);
        Assert.Contains("Batch400FleetCorrelation.SeverityWeight", contract, StringComparison.Ordinal);
        Assert.Contains("Batch300AlertRouting.Decide", contract, StringComparison.Ordinal);
        Assert.Contains("SuggestedRoute", contract, StringComparison.Ordinal);
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
