using Xunit;

namespace Monitor.Web.Tests;

public sealed class B700AuditHistoryTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void AuditAndHistory_ControllerReadsRemainBounded()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationsController.cs");

        Assert.Contains("_audit?.Read(PerformanceScaleOptions.BoundOffset(offset), _performance.BoundAuditLimit(limit))", controller, StringComparison.Ordinal);
        Assert.Contains("_trends?.Read(registrationId, window, PerformanceScaleOptions.BoundOffset(offset), _performance.BoundHistoryLimit(limit))", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlSnapshotQuery", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ISqlServerSnapshotCollector", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditSurface_HasSafeFiltersEmptyStatesAndBoundedPager()
    {
        var audit = Read("src/Monitor.Web/Views/Operations/Audit.cshtml");

        Assert.Contains("name=\"actor\"", audit, StringComparison.Ordinal);
        Assert.Contains("name=\"action\"", audit, StringComparison.Ordinal);
        Assert.Contains("name=\"outcome\"", audit, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"80\"", audit, StringComparison.Ordinal);
        Assert.Contains("_PortalState", audit, StringComparison.Ordinal);
        Assert.Contains("bounded-pager", audit, StringComparison.Ordinal);
        Assert.Contains("Previous page", audit, StringComparison.Ordinal);
        Assert.Contains("Next page", audit, StringComparison.Ordinal);
        Assert.Contains("Credentials and payload contents are never rendered", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionString", audit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception.Message", audit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlConnection", audit, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HistorySurface_HasWindowPagingContextAndTruthfulMissingEvidence()
    {
        var history = Read("src/Monitor.Web/Views/Operations/History.cshtml");

        Assert.Contains("name=\"window\"", history, StringComparison.Ordinal);
        Assert.Contains("name=\"limit\"", history, StringComparison.Ordinal);
        Assert.Contains("ServerDetails", history, StringComparison.Ordinal);
        Assert.Contains("_PortalState", history, StringComparison.Ordinal);
        Assert.Contains("Not collected", history, StringComparison.Ordinal);
        Assert.Contains("AVG MEMORY", history, StringComparison.Ordinal);
        Assert.Contains("PEAK BLOCKED", history, StringComparison.Ordinal);
        Assert.Contains("PEAK RUNNABLE", history, StringComparison.Ordinal);
        Assert.Contains("bounded-pager", history, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", history, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExecuteReader", history, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ISqlSnapshotQuery", history, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditHistoryResponsiveContracts_AreDefined()
    {
        var css = Read("src/Monitor.Web/wwwroot/css/portal.css");

        Assert.Contains(".audit-filter-grid", css, StringComparison.Ordinal);
        Assert.Contains(".audit-detail-grid", css, StringComparison.Ordinal);
        Assert.Contains(".history-window-form", css, StringComparison.Ordinal);
        Assert.Contains(".history-detail-grid", css, StringComparison.Ordinal);
        Assert.Contains(".bounded-pager", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 520px)", css, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
