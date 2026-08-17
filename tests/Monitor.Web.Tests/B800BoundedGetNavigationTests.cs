using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800BoundedGetNavigationTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Alerts_FilterAndPaging_AreBoundedAndPreserveSelection()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationsController.cs");
        var view = Read("src/Monitor.Web/Views/Operations/Alerts.cshtml");

        Assert.Contains("PerformanceScaleOptions.BoundOffset(offset)", controller, StringComparison.Ordinal);
        Assert.Contains("_performance.BoundIncidentLimit(limit)", controller, StringComparison.Ordinal);
        Assert.Contains("NormalizeRuleId(ruleId)", controller, StringComparison.Ordinal);
        Assert.Contains("SecurityInput.NormalizeOptionalToken(ruleId, 80)", controller, StringComparison.Ordinal);

        Assert.Contains("method=\"get\" asp-action=\"Alerts\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"status\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"severity\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"ruleId\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"limit\"", view, StringComparison.Ordinal);

        foreach (var routeToken in new[]
        {
            "asp-route-status=\"@query.Status\"",
            "asp-route-severity=\"@query.Severity\"",
            "asp-route-ruleId=\"@query.RuleId\"",
            "asp-route-limit=\"@query.Limit\""
        })
        {
            Assert.Equal(2, Count(view, routeToken));
        }
        Assert.Contains("asp-route-offset=\"@previousOffset\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-route-offset=\"@nextOffset\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Servers_Paging_IsBoundedBeforeProjection_AndPreservesPageSize()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationsController.cs");
        var readService = Read("src/Monitor.Web/Services/MonitorReadService.cs");
        var view = Read("src/Monitor.Web/Views/Operations/Servers.cshtml");

        Assert.Contains("GetServersPageAsync(offset, limit", controller, StringComparison.Ordinal);
        Assert.Contains("PerformanceScaleOptions.BoundOffset(offset)", readService, StringComparison.Ordinal);
        Assert.Contains("policy.BoundServerLimit(limit)", readService, StringComparison.Ordinal);
        Assert.Contains("Skip(boundedOffset).Take(boundedLimit)", readService, StringComparison.Ordinal);

        Assert.Contains("asp-route-offset=\"@previousOffset\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-route-offset=\"@nextOffset\"", view, StringComparison.Ordinal);
        Assert.Equal(2, Count(view, "asp-route-limit=\"@limit\""));
        Assert.Contains("Navigation never triggers SQL collection", view, StringComparison.Ordinal);
    }

    [Fact]
    public void History_WindowAndPaging_AreBoundedAndPreservedAcrossNavigation()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationsController.cs");
        var view = Read("src/Monitor.Web/Views/Operations/History.cshtml");
        var pager = Section(view, "<nav class=\"bounded-pager\"", "</nav>");

        Assert.Contains("PerformanceScaleOptions.BoundOffset(offset)", controller, StringComparison.Ordinal);
        Assert.Contains("_performance.BoundHistoryLimit(limit)", controller, StringComparison.Ordinal);
        Assert.Contains("name=\"window\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"limit\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"offset\" value=\"0\"", view, StringComparison.Ordinal);
        Assert.Equal(2, Count(pager, "asp-route-window=\"@Model.Window\""));
        Assert.Equal(2, Count(pager, "asp-route-limit=\"@limit\""));
        Assert.Contains("asp-route-offset=\"@Math.Max(0, offset - limit)\"", pager, StringComparison.Ordinal);
        Assert.Contains("asp-route-offset=\"@(offset + limit)\"", pager, StringComparison.Ordinal);
        Assert.Contains("Changing window reads stored aggregates only", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_RemainsReadOnlyAndRoutesHistoryThroughExplicitServerScope()
    {
        var view = Read("src/Monitor.Web/Views/Portal/Reports.cshtml");

        Assert.Contains("Opening this page never contacts a monitored SQL Server", view, StringComparison.Ordinal);
        Assert.Contains("Choose server for history", view, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"Operations\" asp-action=\"Servers\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("method=\"post\"", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static string Section(string value, string startToken, string endToken)
    {
        var start = value.IndexOf(startToken, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Section start token not found: {startToken}");
        var end = value.IndexOf(endToken, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Section end token not found after: {startToken}");
        return value[start..(end + endToken.Length)];
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
