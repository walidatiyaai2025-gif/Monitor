using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class PortalAndTypographyTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void GoogleFontAssets_AreSelfHostedLicensedAndLoadedEverywhere()
    {
        var fonts = Read("src/Monitor.Web/wwwroot/css/fonts.css");
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");
        var login = Read("src/Monitor.Web/Views/Account/Login.cshtml");
        var inter = Path.Combine(Root, "src/Monitor.Web/wwwroot/fonts/inter-variable.ttf");
        var arabic = Path.Combine(Root, "src/Monitor.Web/wwwroot/fonts/noto-sans-arabic-variable.ttf");

        Assert.Contains("Inter", fonts, StringComparison.Ordinal);
        Assert.Contains("Noto Sans Arabic", fonts, StringComparison.Ordinal);
        Assert.DoesNotContain("fonts.googleapis", fonts, StringComparison.OrdinalIgnoreCase);
        Assert.True(new FileInfo(inter).Length > 100_000);
        Assert.True(new FileInfo(arabic).Length > 100_000);
        Assert.True(File.Exists(Path.Combine(Root, "src/Monitor.Web/wwwroot/fonts/OFL-Inter.txt")));
        Assert.True(File.Exists(Path.Combine(Root, "src/Monitor.Web/wwwroot/fonts/OFL-NotoSansArabic.txt")));
        Assert.Contains("~/css/fonts.css", layout, StringComparison.Ordinal);
        Assert.Contains("~/css/fonts.css", login, StringComparison.Ordinal);
    }

    [Fact]
    public void PortalRoutes_HaveDedicatedViewsAndNavigation()
    {
        var routes = typeof(PortalController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(method => method.GetCustomAttributes<HttpGetAttribute>())
            .Select(attribute => attribute.Template)
            .ToArray();
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");

        Assert.Contains("/performance-health", routes);
        Assert.Contains("/recommendations", routes);
        Assert.Contains("/reports", routes);
        Assert.DoesNotContain("href=\"#\"", layout, StringComparison.Ordinal);
        Assert.Contains("Audit Trail", layout, StringComparison.Ordinal);
        Assert.Contains("Operator Help", layout, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Root, "src/Monitor.Web/Views/Portal/Performance.cshtml")));
        Assert.True(File.Exists(Path.Combine(Root, "src/Monitor.Web/Views/Portal/Recommendations.cshtml")));
        Assert.True(File.Exists(Path.Combine(Root, "src/Monitor.Web/Views/Portal/Reports.cshtml")));
    }

    [Fact]
    public void ServerHistoryAndRoleAwareManagementAreDiscoverable()
    {
        var details = Read("src/Monitor.Web/Views/Operations/ServerDetails.cshtml");
        var servers = Read("src/Monitor.Web/Views/Operations/Servers.cshtml");
        Assert.Contains("asp-action=\"History\"", details, StringComparison.Ordinal);
        Assert.Contains("MonitorRoles.Administrator", servers, StringComparison.Ordinal);
        Assert.Contains("Manage targets", servers, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
