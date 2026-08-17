using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05IisBootstrapInstallerTests
{
    [Fact]
    public void Bootstrap_DefaultsToPlanOnlyAndUsesIdempotentCreateOrValidateSemantics()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Bootstrap-IisProductionSingleNode.ps1");

        Assert.Contains("[switch]$Apply", script, StringComparison.Ordinal);
        Assert.Contains("PLAN ONLY", script, StringComparison.Ordinal);
        Assert.Contains("if (-not $Apply)", script, StringComparison.Ordinal);
        Assert.Contains("Get-WindowsFeature", script, StringComparison.Ordinal);
        Assert.Contains("Install-WindowsFeature", script, StringComparison.Ordinal);
        Assert.Contains("Test-Path -LiteralPath $poolPath", script, StringComparison.Ordinal);
        Assert.Contains("Test-Path -LiteralPath $sitePath", script, StringComparison.Ordinal);
        Assert.Contains("Get-WebBinding", script, StringComparison.Ordinal);
        Assert.Contains("No IIS, Windows feature, runtime, certificate, filesystem or ACL changes were made", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_HostingBundleSupportsOfflineAndExplicitMicrosoftOnlineIntegrityChecks()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Bootstrap-IisProductionSingleNode.ps1");

        Assert.Contains("[ValidateSet('Online', 'Offline')]", script, StringComparison.Ordinal);
        Assert.Contains("HostingBundleInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("HostingBundleDownloadUrl", script, StringComparison.Ordinal);
        Assert.Contains("HostingBundleSha256", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-WebRequest", script, StringComparison.Ordinal);
        Assert.Contains("microsoft.com", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Offline Hosting Bundle installation requires -HostingBundleInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("Online Hosting Bundle installation requires an explicit -HostingBundleDownloadUrl", script, StringComparison.Ordinal);
        Assert.Contains("/quiet", script, StringComparison.Ordinal);
        Assert.Contains("/norestart", script, StringComparison.Ordinal);
        Assert.Contains("3010", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_CertificateAndIisIdentitySetupStaySecretFreeAndFailClosed()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Bootstrap-IisProductionSingleNode.ps1");

        Assert.Contains("[Security.SecureString]$CertificatePfxPassword", script, StringComparison.Ordinal);
        Assert.Contains("Import-PfxCertificate", script, StringComparison.Ordinal);
        Assert.Contains("Cert:\\LocalMachine\\My", script, StringComparison.Ordinal);
        Assert.Contains("New-WebAppPool", script, StringComparison.Ordinal);
        Assert.Contains("managedRuntimeVersion", script, StringComparison.Ordinal);
        Assert.Contains("ApplicationPoolIdentity", script, StringComparison.Ordinal);
        Assert.Contains("SpecificUser", script, StringComparison.Ordinal);
        Assert.Contains("LocalSystem", script, StringComparison.Ordinal);
        Assert.Contains("LocalService", script, StringComparison.Ordinal);
        Assert.Contains("NetworkService", script, StringComparison.Ordinal);
        Assert.Contains("New-Website", script, StringComparison.Ordinal);
        Assert.Contains("New-WebBinding", script, StringComparison.Ordinal);
        Assert.Contains("AddSslCertificate", script, StringComparison.Ordinal);
        Assert.Contains("C:\\Program Files\\Monitor\\releases", script, StringComparison.Ordinal);
        Assert.Contains("C:\\ProgramData\\Monitor\\App_Data", script, StringComparison.Ordinal);
        Assert.Contains("icacls.exe", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[string]$CertificatePfxPassword", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[string]$Password", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bootstrap_FreshHostHardeningStopsForRebootAndProtectsSharedIisState()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Bootstrap-IisProductionSingleNode.ps1");

        Assert.Contains("Get-DotNetExecutable", script, StringComparison.Ordinal);
        Assert.Contains("aspnetcorev2_outofprocess.dll", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("download.visualstudio.microsoft.com", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("builds.dotnet.microsoft.com", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[switch]$AllowIisServiceRestart", script, StringComparison.Ordinal);
        Assert.Contains("Restart-IisServicesAfterHostingBundle", script, StringComparison.Ordinal);
        Assert.Contains("& net.exe stop was /y", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("& net.exe start w3svc", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RebootRequired", script, StringComparison.Ordinal);
        Assert.Contains("Reboot the server and rerun", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("valid Microsoft Authenticode signature", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("will not silently restart shared IIS services", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bootstrap will not overwrite an unexpected binding certificate", script, StringComparison.OrdinalIgnoreCase);

        var pfxGuard = script.IndexOf("if (-not (Test-Path -LiteralPath $storePath))", StringComparison.Ordinal);
        var pfxImport = script.IndexOf("Import-PfxCertificate", StringComparison.Ordinal);
        Assert.True(pfxGuard >= 0 && pfxImport > pfxGuard,
            "PFX import must remain conditional so reruns reuse an already-installed matching machine certificate.");
    }

    [Fact]
    public void Installer_OrdersBootstrapPreflightDeployAndRequiresDurableReleaseAcknowledgementForApply()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Install-ProductionSingleNode.ps1");

        var bootstrap = script.IndexOf("& $bootstrapScript", StringComparison.Ordinal);
        var preflight = script.IndexOf("& $preflightScript", StringComparison.Ordinal);
        var deploy = script.IndexOf("& $deployScript", StringComparison.Ordinal);

        Assert.True(bootstrap >= 0, "Installer must invoke the bootstrap script.");
        Assert.True(preflight > bootstrap, "Existing production preflight must run after bootstrap.");
        Assert.True(deploy > preflight, "Existing deployment automation must run after authoritative preflight.");
        Assert.Contains("[switch]$Apply", script, StringComparison.Ordinal);
        Assert.Contains("[switch]$AcknowledgeDurableReleasePrerequisite", script, StringComparison.Ordinal);
        Assert.Contains("#162 durable RC publication + independent verification is complete", script, StringComparison.Ordinal);
        Assert.Contains("This switch does not itself satisfy or verify #162", script, StringComparison.Ordinal);
        Assert.Contains("Deploy-ProductionSingleNode.ps1", script, StringComparison.Ordinal);
        Assert.Contains("Test-IisProductionPrerequisites.ps1", script, StringComparison.Ordinal);
        Assert.Contains("Bootstrap-IisProductionSingleNode.ps1", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_PowerShell7PrerequisiteIsPinnedVerifiedAndStopsBeforeIisMutation()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Install-ProductionSingleNode.ps1");

        Assert.Contains("[ValidateSet('Online', 'Offline')]", script, StringComparison.Ordinal);
        Assert.Contains("$PowerShellMode = 'Online'", script, StringComparison.Ordinal);
        Assert.Contains("https://github.com/PowerShell/PowerShell/releases/download/v7.4.16/PowerShell-7.4.16-win-x64.msi", script, StringComparison.Ordinal);
        Assert.Contains("2C0C2036B0032375AD4F7809A92D0B6FA4A8E4EE89A75211514C4CF55AE22495", script, StringComparison.Ordinal);
        Assert.Contains("Test-PowerShellDownloadUri", script, StringComparison.Ordinal);
        Assert.Contains("github.com", script, StringComparison.Ordinal);
        Assert.Contains("/PowerShell/PowerShell/releases/download/", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.Contains("O=Microsoft Corporation", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("msiexec.exe", script, StringComparison.Ordinal);
        Assert.Contains("'/qn'", script, StringComparison.Ordinal);
        Assert.Contains("'/norestart'", script, StringComparison.Ordinal);
        Assert.Contains("ADD_PATH=1", script, StringComparison.Ordinal);
        Assert.Contains("Offline PowerShell 7 installation requires -PowerShellMsiInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("No IIS mutation was attempted", script, StringComparison.Ordinal);
        Assert.Contains("$PSVersionTable.PSVersion.Major -ge 7", script, StringComparison.Ordinal);

        var ensure = script.IndexOf("$powerShell7 = Ensure-PowerShell7", StringComparison.Ordinal);
        var relaunchGuard = script.IndexOf("if ($Apply -and [bool]$powerShell7.RequiresRelaunch)", StringComparison.Ordinal);
        var bootstrap = script.IndexOf("$bootstrap = & $bootstrapScript", StringComparison.Ordinal);
        Assert.True(ensure >= 0 && relaunchGuard > ensure && bootstrap > relaunchGuard,
            "PowerShell 7 installation/relaunch must be resolved before the first IIS bootstrap mutation can execute.");
    }

    [Fact]
    public void ProductionCandidate_ParsesAndPackagesBootstrapAndInstallerEntrypoint()
    {
        var root = FindRepoRoot();
        var workflow = Read(root, ".github/workflows/production-candidate.yml");

        foreach (var script in new[]
                 {
                     "scripts/Bootstrap-IisProductionSingleNode.ps1",
                     "scripts/Install-ProductionSingleNode.ps1"
                 })
        {
            Assert.Contains($"- '{script}'", workflow, StringComparison.Ordinal);
            Assert.Contains($"'{script}'", workflow, StringComparison.Ordinal);
            Assert.Contains($"Copy-Item {script} \"$ops/scripts/\"", workflow, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void IisRunbook_DocumentsDryRunOnlineOfflinePfxAndDurableReleaseBoundary()
    {
        var root = FindRepoRoot();
        var guide = Read(root, "docs/DEPLOY_IIS.md");

        Assert.Contains("Bootstrap-IisProductionSingleNode.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("Install-ProductionSingleNode.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("PLAN ONLY", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HostingBundleMode Online", guide, StringComparison.Ordinal);
        Assert.Contains("HostingBundleMode Offline", guide, StringComparison.Ordinal);
        Assert.Contains("CertificatePfxPath", guide, StringComparison.Ordinal);
        Assert.Contains("CertificateThumbprint", guide, StringComparison.Ordinal);
        Assert.Contains("AcknowledgeDurableReleasePrerequisite", guide, StringComparison.Ordinal);
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
