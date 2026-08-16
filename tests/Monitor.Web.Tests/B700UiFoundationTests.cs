using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B700UiFoundationTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ErrorController_ExposesOnlySafeStatusSurfaces()
    {
        Assert.NotNull(typeof(ErrorController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true).SingleOrDefault());

        var source = Read("src/Monitor.Web/Controllers/ErrorController.cs");
        Assert.Contains("/error", source, StringComparison.Ordinal);
        Assert.Contains("/access-denied", source, StringComparison.Ordinal);
        Assert.Contains("/error/status/{statusCode:int}", source, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status500InternalServerError", source, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status404NotFound", source, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status403Forbidden", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IExceptionHandlerFeature", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception.Message", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ServerError.cshtml")]
    [InlineData("AccessDenied.cshtml")]
    [InlineData("NotFound.cshtml")]
    public void ErrorViews_DoNotRenderSensitiveDiagnostics(string file)
    {
        var source = Read($"src/Monitor.Web/Views/Error/{file}");
        Assert.Contains("_ErrorLayout", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception.Message", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PortalShell_HasBoundaryAwareNavigationAndKeyboardMobileToggle()
    {
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");
        var script = Read("src/Monitor.Web/wwwroot/js/site.js");
        var css = Read("src/Monitor.Web/wwwroot/css/portal.css");

        Assert.Contains("path == value || path.StartsWith", layout, StringComparison.Ordinal);
        Assert.Contains("data-nav-toggle", layout, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", layout, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"primary-navigation\"", layout, StringComparison.Ordinal);
        Assert.Contains("event.key === 'Escape'", script, StringComparison.Ordinal);
        Assert.Contains("setAttribute('aria-expanded'", script, StringComparison.Ordinal);
        Assert.Contains(".sidebar.is-open", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 860px)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PortalFoundation_DefinesReusablePageAndStateContracts()
    {
        var models = Read("src/Monitor.Web/Models/PortalUiModels.cs");
        var state = Read("src/Monitor.Web/Views/Shared/_PortalState.cshtml");
        var header = Read("src/Monitor.Web/Views/Shared/_PortalPageHeader.cshtml");
        var css = Read("src/Monitor.Web/wwwroot/css/portal.css");

        Assert.Contains("PortalStateViewModel", models, StringComparison.Ordinal);
        Assert.Contains("PortalPageHeaderViewModel", models, StringComparison.Ordinal);
        Assert.Contains("portal-state", state, StringComparison.Ordinal);
        Assert.Contains("portal-page-heading", header, StringComparison.Ordinal);
        Assert.Contains(".responsive-table", css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Batch700Ledger_PreservesProductionTruthBoundaries()
    {
        var ledger = Read("docs/BATCH_700.md");
        Assert.Contains("cache/control-plane only", ledger, StringComparison.Ordinal);
        Assert.Contains("No autonomous remediation", ledger, StringComparison.Ordinal);
        Assert.Contains("No credential, connection-string, SQL text", ledger, StringComparison.Ordinal);
        Assert.Contains("390px mobile", ledger, StringComparison.Ordinal);
        Assert.Contains("#116/#111", ledger, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
