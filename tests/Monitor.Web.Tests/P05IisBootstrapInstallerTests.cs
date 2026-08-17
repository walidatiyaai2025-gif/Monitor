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
        Assert.Contains("ProductType -eq 1", script, StringComparison.Ordinal);
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
        Assert.Contains("download.visualstudio.microsoft.com", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("builds.dotnet.microsoft.com", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Offline Hosting Bundle installation requires -HostingBundleInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("Online Hosting Bundle installation requires an explicit -HostingBundleDownloadUrl", script, StringComparison.Ordinal);
        Assert.Contains("/quiet", script, StringComparison.Ordinal);
        Assert.Contains("/norestart", script, StringComparison.Ordinal);
        Assert.Contains("3010", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_RequiresPowerShell7BeforeAnyIisMutationAndPinsItsMsi()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Install-ProductionSingleNode.ps1");

        Assert.Contains("[ValidateSet('Online', 'Offline')]", script, StringComparison.Ordinal);
        Assert.Contains("PowerShellMsiInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("PowerShell-7.4.16-win-x64.msi", script, StringComparison.Ordinal);
        Assert.Contains("2C0C2036B0032375AD4F7809A92D0B6FA4A8E4EE89A75211514C4CF55AE22495", script, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.Contains("Microsoft Corporation Authenticode signature", script, StringComparison.Ordinal);
        Assert.Contains("PowerShell 7 was installed successfully", script, StringComparison.Ordinal);
        Assert.Contains("No IIS mutation was attempted", script, StringComparison.Ordinal);
        Assert.Contains("requires PowerShell 7", script, StringComparison.Ordinal);

        var ensurePowerShell = script.IndexOf("$powerShell7 = Ensure-PowerShell7", StringComparison.Ordinal);
        var bootstrap = script.IndexOf("$bootstrap = & $bootstrapScript", StringComparison.Ordinal);
        Assert.True(ensurePowerShell >= 0, "Installer must evaluate PowerShell 7 before bootstrap.");
        Assert.True(bootstrap > ensurePowerShell, "IIS bootstrap must not run until the PowerShell 7 prerequisite is handled.");
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
    public void Bootstrap_RequiresDedicatedAppPoolAndExactSniBinding()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Bootstrap-IisProductionSingleNode.ps1");

        Assert.Contains("Get-Website", script, StringComparison.Ordinal);
        Assert.Contains("shared by other IIS site(s)", script, StringComparison.Ordinal);
        Assert.Contains("requires a dedicated app pool", script, StringComparison.Ordinal);
        Assert.Contains("SslFlags 1", script, StringComparison.Ordinal);
        Assert.Contains("Set-WebBinding", script, StringComparison.Ordinal);
        Assert.Contains("sslFlags=1", script, StringComparison.Ordinal);
        Assert.Contains("must already use exact SNI sslFlags=1", script, StringComparison.Ordinal);
        Assert.Contains("will not silently rewrite unexpected SSL binding semantics", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_DoesNotSilentlyRestartExistingIisAfterHostingBundleInstallation()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Bootstrap-IisProductionSingleNode.ps1");
        var installer = Read(root, "scripts/Install-ProductionSingleNode.ps1");

        Assert.Contains("[switch]$AllowIisServiceRestart", script, StringComparison.Ordinal);
        Assert.Contains("approved maintenance window", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("will not silently restart shared IIS services", script, StringComparison.Ordinal);
        Assert.Contains("net.exe stop was /y", script, StringComparison.Ordinal);
        Assert.Contains("net.exe start w3svc", script, StringComparison.Ordinal);
        Assert.Contains("IisServicesRestarted", script, StringComparison.Ordinal);
        Assert.Contains("[switch]$AllowIisServiceRestart", installer, StringComparison.Ordinal);
        Assert.Contains("$bootstrapParameters.AllowIisServiceRestart = $true", installer, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_OrdersBootstrapPreflightDeployAndRequiresDurableReleaseAcknowledgementForApply()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Install-ProductionSingleNode.ps1");

        var bootstrap = script.IndexOf("$bootstrap = & $bootstrapScript", StringComparison.Ordinal);
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
    public void IisRunbook_DocumentsDryRunOnlineOfflinePfxPowerShellAndDurableReleaseBoundary()
    {
        var root = FindRepoRoot();
        var guide = Read(root, "docs/DEPLOY_IIS.md");

        Assert.Contains("Bootstrap-IisProductionSingleNode.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("Install-ProductionSingleNode.ps1", guide, StringComparison.Ordinal);
        Assert.Contains("PLAN ONLY", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HostingBundleMode Online", guide, StringComparison.Ordinal);
        Assert.Contains("HostingBundleMode Offline", guide, StringComparison.Ordinal);
        Assert.Contains("PowerShellMode Online", guide, StringComparison.Ordinal);
        Assert.Contains("PowerShellMode Offline", guide, StringComparison.Ordinal);
        Assert.Contains("PowerShell-7.4.16-win-x64.msi", guide, StringComparison.Ordinal);
        Assert.Contains("AllowIisServiceRestart", guide, StringComparison.Ordinal);
        Assert.Contains("dedicated application pool", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sslFlags=1", guide, StringComparison.Ordinal);
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
