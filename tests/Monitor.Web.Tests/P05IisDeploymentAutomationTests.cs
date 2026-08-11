using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05IisDeploymentAutomationTests
{
    [Fact]
    public void IisPreflight_IsReadOnlyAndRequiresApprovedHttpsIdentityAndCertificate()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Test-IisProductionPrerequisites.ps1");

        Assert.Contains("WebAdministration", script, StringComparison.Ordinal);
        Assert.Contains("Microsoft.AspNetCore.App 8", script, StringComparison.Ordinal);
        Assert.Contains("Asp.Net Core Module", script, StringComparison.Ordinal);
        Assert.Contains("No Managed Code", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LocalSystem", script, StringComparison.Ordinal);
        Assert.Contains("LocalService", script, StringComparison.Ordinal);
        Assert.Contains("NetworkService", script, StringComparison.Ordinal);
        Assert.Contains("Cert:\\LocalMachine\\My", script, StringComparison.Ordinal);
        Assert.Contains("Get-WebBinding", script, StringComparison.Ordinal);
        Assert.Contains("certificateHash", script, StringComparison.Ordinal);
        Assert.Contains("No configuration was changed", script, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("New-WebBinding", script, StringComparison.Ordinal);
        Assert.DoesNotContain("New-Website", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-ItemProperty", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop-WebAppPool", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-WebAppPool", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", script, StringComparison.Ordinal);
    }

    [Fact]
    public void IisDeploy_IsPlanOnlyByDefaultAndRequiresExplicitApply()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Deploy-ProductionSingleNode.ps1");

        Assert.Contains("[switch]$Apply", script, StringComparison.Ordinal);
        Assert.Contains("PLAN ONLY", script, StringComparison.Ordinal);
        Assert.Contains("if (-not $Apply)", script, StringComparison.Ordinal);
        Assert.Contains("No IIS, filesystem, ACL, application-pool, binding, certificate, configuration or state changes were made", script, StringComparison.Ordinal);
        Assert.Contains("Test-IisProductionPrerequisites.ps1", script, StringComparison.Ordinal);
        Assert.Contains("OperationalBackupId is required", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("SHA-256 mismatch", script, StringComparison.Ordinal);
    }

    [Fact]
    public void IisDeploy_PreservesDurableStateAcrossVersionedReleases()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Deploy-ProductionSingleNode.ps1");

        Assert.Contains("C:\\Program Files\\Monitor\\releases", script, StringComparison.Ordinal);
        Assert.Contains("C:\\ProgramData\\Monitor\\App_Data", script, StringComparison.Ordinal);
        Assert.Contains("StateRoot must be outside ReleaseRoot", script, StringComparison.Ordinal);
        Assert.Contains("New-Item -ItemType Junction", script, StringComparison.Ordinal);
        Assert.Contains("registrations, secrets, key rings, backups or operational state", script, StringComparison.Ordinal);
        Assert.Contains("deployment-current.json", script, StringComparison.Ordinal);
        Assert.Contains("previousPhysicalPath", script, StringComparison.Ordinal);
        Assert.Contains("Automatic application-path rollback", script, StringComparison.Ordinal);

        Assert.DoesNotContain("Remove-Item -LiteralPath $stateRoot", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Remove-Item -LiteralPath $StateRoot", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Remove-Item $stateRoot", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Remove-Item $StateRoot", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IisDeploy_RequiresSecretFreeSingleNodeConfigAndDoesNotAcceptCredentialArguments()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Deploy-ProductionSingleNode.ps1");

        Assert.Contains("Deployment:Mode to SingleNode", script, StringComparison.Ordinal);
        Assert.Contains("DevelopmentAdmin credential material", script, StringComparison.Ordinal);
        Assert.Contains("AllowedHosts must not use a wildcard", script, StringComparison.Ordinal);
        Assert.Contains("Password|HashBase64|SaltBase64|ConnectionString", script, StringComparison.Ordinal);
        Assert.Contains("approved environment variables", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[string]$Password", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[SecureString]$Password", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("New-WebBinding", script, StringComparison.Ordinal);
        Assert.DoesNotContain("New-Website", script, StringComparison.Ordinal);
    }

    [Fact]
    public void IisDeploy_SwitchesOnlyAfterStagingAndRollsBackOnAcceptanceFailure()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Deploy-ProductionSingleNode.ps1");
        var move = script.IndexOf("Move-Item -LiteralPath $stagingPath -Destination $releasePath", StringComparison.Ordinal);
        var switchPath = script.IndexOf("Set-ItemProperty -LiteralPath \"IIS:\\Sites\\$SiteName\" -Name physicalPath -Value $releasePath", StringComparison.Ordinal);
        var accept = script.IndexOf("Accept-ProductionSingleNode.ps1", StringComparison.Ordinal);
        var rollback = script.LastIndexOf("Set-ItemProperty -LiteralPath \"IIS:\\Sites\\$SiteName\" -Name physicalPath -Value $previousPhysicalPath", StringComparison.Ordinal);

        Assert.True(move >= 0, "Candidate must be staged into its immutable release path.");
        Assert.True(switchPath > move, "IIS must switch only after the candidate is fully staged.");
        Assert.True(accept > switchPath, "HTTPS acceptance must run after IIS points at the new candidate.");
        Assert.True(rollback > accept, "Failure handling must restore the previous IIS physical path.");
        Assert.Contains("https://$HostName", script, StringComparison.Ordinal);
        Assert.DoesNotContain("http://$HostName", script, StringComparison.Ordinal);
    }

    [Fact]
    public void IisRunbook_ReferencesPreflightPlanApplyAcceptanceAndRollback()
    {
        var root = FindRepoRoot();
        var guide = Read(root, "docs/DEPLOY_IIS.md");

        Assert.Contains("Test-IisProductionPrerequisites.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("Deploy-ProductionSingleNode.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("PLAN ONLY", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-Apply", guide, StringComparison.Ordinal);
        Assert.Contains("Accept-ProductionSingleNode.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("ROLLBACK_RUNBOOK.md", guide, StringComparison.Ordinal);
        Assert.Contains("App_Data", guide, StringComparison.Ordinal);
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepoRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for P0.5 IIS deployment automation tests.");
    }
}
