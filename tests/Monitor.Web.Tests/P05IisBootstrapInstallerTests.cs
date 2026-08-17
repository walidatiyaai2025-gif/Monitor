using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05IisBootstrapInstallerTests
{
    [Fact]
    public void Bootstrap_IsPlanOnlyByDefaultAndApplyIsExplicit()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Initialize-IisProductionHost.ps1");

        Assert.Contains("[switch]$Apply", script, StringComparison.Ordinal);
        Assert.Contains("PLAN ONLY", script, StringComparison.Ordinal);
        Assert.Contains("if (-not $Apply)", script, StringComparison.Ordinal);
        Assert.Contains("No Windows feature, runtime, IIS, certificate, binding, filesystem or ACL changes were made", script, StringComparison.Ordinal);
        Assert.Contains("Applying the IIS host bootstrap requires an elevated PowerShell session", script, StringComparison.Ordinal);
        Assert.Contains("Get-WindowsFeature", script, StringComparison.Ordinal);
        Assert.Contains("Install-WindowsFeature", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_HandlesHostingBundleOnlineOfflineAndOptionalShaPin()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Initialize-IisProductionHost.ps1");

        Assert.Contains("ValidateSet('Auto', 'Online', 'Offline')", script, StringComparison.Ordinal);
        Assert.Contains("HostingBundleInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("HostingBundleUrl", script, StringComparison.Ordinal);
        Assert.Contains("HostingBundleSha256", script, StringComparison.Ordinal);
        Assert.Contains("download.visualstudio.microsoft.com", script, StringComparison.Ordinal);
        Assert.Contains("builds.dotnet.microsoft.com", script, StringComparison.Ordinal);
        Assert.Contains("HostingBundleUrl must use HTTPS", script, StringComparison.Ordinal);
        Assert.Contains("not an approved Microsoft download host", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("SHA-256 mismatch", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-WebRequest", script, StringComparison.Ordinal);
        Assert.Contains("/install", script, StringComparison.Ordinal);
        Assert.Contains("/quiet", script, StringComparison.Ordinal);
        Assert.Contains("/norestart", script, StringComparison.Ordinal);
        Assert.Contains("3010", script, StringComparison.Ordinal);
        Assert.Contains("Microsoft\\.AspNetCore\\.App 8\\.", script, StringComparison.Ordinal);
        Assert.Contains("Asp.Net Core Module", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_UsesSecureCertificateInputsAndFailClosedHttpsBinding()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Initialize-IisProductionHost.ps1");

        Assert.Contains("[Security.SecureString]$PfxPassword", script, StringComparison.Ordinal);
        Assert.Contains("Import-PfxCertificate", script, StringComparison.Ordinal);
        Assert.Contains("Cert:\\LocalMachine\\My", script, StringComparison.Ordinal);
        Assert.Contains("Specify exactly one certificate source", script, StringComparison.Ordinal);
        Assert.Contains("Get-WebBinding", script, StringComparison.Ordinal);
        Assert.Contains("New-WebBinding", script, StringComparison.Ordinal);
        Assert.Contains("AddSslCertificate", script, StringComparison.Ordinal);
        Assert.Contains("Refusing to replace it implicitly", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[string]$PfxPassword", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConvertTo-SecureString -AsPlainText", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bootstrap_CreatesOnlyApprovedLowPrivilegeIisBaselineAndStableRoots()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Initialize-IisProductionHost.ps1");

        Assert.Contains("New-WebAppPool", script, StringComparison.Ordinal);
        Assert.Contains("No Managed Code", script, StringComparison.Ordinal);
        Assert.Contains("ApplicationPoolIdentity", script, StringComparison.Ordinal);
        Assert.Contains("LocalSystem", script, StringComparison.Ordinal);
        Assert.Contains("LocalService", script, StringComparison.Ordinal);
        Assert.Contains("NetworkService", script, StringComparison.Ordinal);
        Assert.Contains("New-Website", script, StringComparison.Ordinal);
        Assert.Contains("C:\\Program Files\\Monitor\\releases", script, StringComparison.Ordinal);
        Assert.Contains("C:\\ProgramData\\Monitor\\App_Data", script, StringComparison.Ordinal);
        Assert.Contains("icacls.exe", script, StringComparison.Ordinal);
        Assert.Contains("(OI)(CI)M", script, StringComparison.Ordinal);
        Assert.Contains("(OI)(CI)RX", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item -LiteralPath $StateRoot", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Remove-Item -LiteralPath $ReleaseRoot", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstallerEntryPoint_OrdersBootstrapThenAuthoritativePreflightThenExistingDeploy()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Install-ProductionSingleNode.ps1");
        var bootstrap = script.IndexOf("& $bootstrapScript @bootstrapArgs", StringComparison.Ordinal);
        var preflight = script.IndexOf("& $preflightScript", StringComparison.Ordinal);
        var deploy = script.IndexOf("& $deployScript @deployArgs", StringComparison.Ordinal);

        Assert.True(bootstrap >= 0, "Single entrypoint must invoke the bootstrap.");
        Assert.True(preflight > bootstrap, "Existing production preflight must remain authoritative after bootstrap.");
        Assert.True(deploy > preflight, "Existing deployment must run only after the authoritative preflight.");
        Assert.Contains("Initialize-IisProductionHost.ps1", script, StringComparison.Ordinal);
        Assert.Contains("Test-IisProductionPrerequisites.ps1", script, StringComparison.Ordinal);
        Assert.Contains("Deploy-ProductionSingleNode.ps1", script, StringComparison.Ordinal);
        Assert.Contains("[switch]$Apply", script, StringComparison.Ordinal);
        Assert.Contains("PLAN ONLY", script, StringComparison.Ordinal);
        Assert.Contains("OperationalBackupId is required", script, StringComparison.Ordinal);
        Assert.Contains("existing SingleNode deployment", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BootstrapAndEntryPoint_DoNotPublishOrBypassDurableRc61Gate()
    {
        var root = FindRepoRoot();
        var combined = Read(root, "scripts/Initialize-IisProductionHost.ps1") + "\n" +
                       Read(root, "scripts/Install-ProductionSingleNode.ps1");

        Assert.DoesNotContain("workflow_dispatch", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("promote-existing-candidate", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verify-durable-release", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("v0.1.0-rc.61", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh release", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("New-GitHubRelease", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BootstrapRunbook_ShowsDryRunOnlineOfflineCertificateAndDeployExamples()
    {
        var root = FindRepoRoot();
        var guide = Read(root, "docs/IIS_BOOTSTRAP_INSTALLER.md");

        Assert.Contains("Initialize-IisProductionHost.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("Install-ProductionSingleNode.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("PLAN ONLY", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Online", guide, StringComparison.Ordinal);
        Assert.Contains("Offline", guide, StringComparison.Ordinal);
        Assert.Contains("HostingBundleSha256", guide, StringComparison.Ordinal);
        Assert.Contains("CertificateThumbprint", guide, StringComparison.Ordinal);
        Assert.Contains("PfxPath", guide, StringComparison.Ordinal);
        Assert.Contains("ConvertTo-SecureString", guide, StringComparison.Ordinal);
        Assert.Contains("Test-IisProductionPrerequisites.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("Deploy-ProductionSingleNode.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("#162", guide, StringComparison.Ordinal);
        Assert.Contains("#116", guide, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for P0.5 IIS bootstrap installer tests.");
    }
}
