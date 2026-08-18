using Xunit;

namespace Monitor.Web.Tests;

public sealed class UatKeepWarmAndRefreshAllAcceptanceTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ConnectionLab_RefreshAllControl_UsesExistingProtectedRefreshEndpointSequentially()
    {
        var view = File.ReadAllText(Path.Combine(Root, "src", "Monitor.Web", "Views", "ConnectionLab", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(Root, "src", "Monitor.Web", "wwwroot", "js", "site.js"));

        Assert.Contains("id=\"target-@registration.Id\"", view, StringComparison.Ordinal);
        Assert.Contains("setupRefreshAllConnections", script, StringComparison.Ordinal);
        Assert.Contains("article[id^=\"target-\"]", script, StringComparison.Ordinal);
        Assert.Contains("input[name=\"__RequestVerificationToken\"]", script, StringComparison.Ordinal);
        Assert.Contains("for (let index = 0; index < registrationIds.length; index += 1)", script, StringComparison.Ordinal);
        Assert.Contains("/refresh-snapshot`,", script, StringComparison.Ordinal);
        Assert.Contains("method: 'POST'", script, StringComparison.Ordinal);
        Assert.Contains("credentials: 'same-origin'", script, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken: tokenInput.value", script, StringComparison.Ordinal);
        Assert.Contains("response.status === 409", script, StringComparison.Ordinal);
        Assert.Contains("response.status === 429", script, StringComparison.Ordinal);
        Assert.Contains("Refresh all connections", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishedPackage_IncludesIdempotentIisKeepWarmHelper()
    {
        var project = File.ReadAllText(Path.Combine(Root, "src", "Monitor.Web", "Monitor.Web.csproj"));
        var helperPath = Path.Combine(Root, "src", "Monitor.Web", "Operations", "Set-IisAlwaysWarm.ps1");
        var helper = File.ReadAllText(helperPath);

        Assert.Contains("Operations\\Set-IisAlwaysWarm.ps1", project, StringComparison.Ordinal);
        Assert.Contains("_operations\\scripts\\Set-IisAlwaysWarm.ps1", project, StringComparison.Ordinal);
        Assert.Contains("CopyToPublishDirectory=\"PreserveNewest\"", project, StringComparison.Ordinal);

        Assert.Contains("Web-AppInit", helper, StringComparison.Ordinal);
        Assert.Contains("startMode -Value 'AlwaysRunning'", helper, StringComparison.Ordinal);
        Assert.Contains("processModel.idleTimeout -Value ([TimeSpan]::Zero)", helper, StringComparison.Ordinal);
        Assert.Contains("serverAutoStart -Value $true", helper, StringComparison.Ordinal);
        Assert.Contains("/preloadEnabled:true", helper, StringComparison.Ordinal);
        Assert.Contains("Routine IIS periodic recycling remains unchanged.", helper, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
