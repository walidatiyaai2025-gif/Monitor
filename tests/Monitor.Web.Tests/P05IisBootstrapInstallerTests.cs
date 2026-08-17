using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05IisBootstrapInstallerTests
{
    [Fact]
    public void Bootstrap_IsPlanOnlyByDefaultAndInstallsOnlyExplicitServerPrerequisites()
    {
        var root = FindRepoRoot();
        var setup = Read(root, "scripts/Setup-MonitorServer.ps1");

        Assert.Contains("[switch]$Apply", setup, StringComparison.Ordinal);
        Assert.Contains("PLAN ONLY", setup, StringComparison.Ordinal);
        Assert.Contains("Install-WindowsFeature", setup, StringComparison.Ordinal);
        Assert.Contains("Web-Scripting-Tools", setup, StringComparison.Ordinal);
        Assert.Contains("Microsoft\\.AspNetCore\\.App 8\\.", setup, StringComparison.Ordinal);
        Assert.Contains("Asp.Net Core Module\\V2\\aspnetcorev2.dll", setup, StringComparison.Ordinal);
        Assert.Contains("dotnet-hosting-win.exe", setup, StringComparison.Ordinal);
        Assert.Contains("PowerShell-7.4.16-win-x64.msi", setup, StringComparison.Ordinal);
        Assert.Contains("PowerShellMsiPath", setup, StringComparison.Ordinal);
        Assert.Contains("Offline mode", setup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Get-FileHash", setup, StringComparison.Ordinal);
        Assert.Contains("SHA-256 mismatch", setup, StringComparison.Ordinal);
        Assert.Contains("Test-IisProductionPrerequisites.ps1", setup, StringComparison.Ordinal);

        Assert.DoesNotContain("New-SelfSignedCertificate", setup, StringComparison.Ordinal);
        Assert.DoesNotContain("[string]$CertificatePfxPassword", setup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Security.SecureString]$CertificatePfxPassword", setup, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_PreservesApprovedCertificateAndLowPrivilegeIisIdentityBoundaries()
    {
        var root = FindRepoRoot();
        var setup = Read(root, "scripts/Setup-MonitorServer.ps1");

        Assert.Contains("The supplied PFX does not contain approved certificate", setup, StringComparison.Ordinal);
        Assert.Contains("Cert:\\LocalMachine\\My", setup, StringComparison.Ordinal);
        Assert.Contains("ApplicationPoolIdentity", setup, StringComparison.Ordinal);
        Assert.Contains("LocalSystem", setup, StringComparison.Ordinal);
        Assert.Contains("LocalService", setup, StringComparison.Ordinal);
        Assert.Contains("NetworkService", setup, StringComparison.Ordinal);
        Assert.Contains("will not be hijacked", setup, StringComparison.Ordinal);
        Assert.Contains("New-WebBinding", setup, StringComparison.Ordinal);
        Assert.Contains("AddSslCertificate", setup, StringComparison.Ordinal);
        Assert.Contains("${aclIdentity}:(OI)(CI)M", setup, StringComparison.Ordinal);
        Assert.Contains("${aclIdentity}:(OI)(CI)RX", setup, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_DoesNotSilentlyRestartExistingIisHostAfterHostingBundleInstall()
    {
        var root = FindRepoRoot();
        var setup = Read(root, "scripts/Setup-MonitorServer.ps1");

        Assert.Contains("AllowIisServiceRestart", setup, StringComparison.Ordinal);
        Assert.Contains("approved maintenance window", setup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("net.exe stop was /y", setup, StringComparison.Ordinal);
        Assert.Contains("net.exe start w3svc", setup, StringComparison.Ordinal);
        Assert.Contains("IisWasInstalledBefore -and -not $AllowIisServiceRestart", setup, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_VerifiesPackageThenRunsBootstrapPreflightAndExistingRollbackCapableDeploy()
    {
        var root = FindRepoRoot();
        var installer = Read(root, "scripts/Install-Monitor.ps1");

        Assert.Contains("Get-FileHash", installer, StringComparison.Ordinal);
        Assert.Contains("SHA-256 mismatch", installer, StringComparison.Ordinal);
        Assert.Contains("Setup-MonitorServer.ps1", installer, StringComparison.Ordinal);
        Assert.Contains("Test-IisProductionPrerequisites.ps1", installer, StringComparison.Ordinal);
        Assert.Contains("Deploy-ProductionSingleNode.ps1", installer, StringComparison.Ordinal);
        Assert.Contains("ContinueAfterBootstrap", installer, StringComparison.Ordinal);
        Assert.Contains("PowerShell 7 is required", installer, StringComparison.Ordinal);
        Assert.Contains("No application release was deployed", installer, StringComparison.Ordinal);

        var setupIndex = installer.IndexOf("& $setupScript", StringComparison.Ordinal);
        var deploymentIndex = installer.LastIndexOf("Invoke-AuthoritativeDeployment", StringComparison.Ordinal);
        Assert.True(setupIndex >= 0, "Installer must invoke bootstrap setup.");
        Assert.True(deploymentIndex > setupIndex, "Existing authoritative deployment must run only after bootstrap.");
    }

    [Fact]
    public void ProductionCandidate_ParsesAndPackagesBootstrapInstallerTooling()
    {
        var root = FindRepoRoot();
        var project = Read(root, "src/Monitor.Web/Monitor.Web.csproj");
        var validator = Read(root, "scripts/Test-ProductionCandidate.ps1");

        Assert.Contains("_operations/scripts/Setup-MonitorServer.ps1", project, StringComparison.Ordinal);
        Assert.Contains("_operations/scripts/Install-Monitor.ps1", project, StringComparison.Ordinal);
        Assert.Contains("_operations/docs/MONITOR_SERVER_SETUP.md", project, StringComparison.Ordinal);
        Assert.Contains("System.Management.Automation.Language.Parser", validator, StringComparison.Ordinal);
        Assert.Contains("Setup-MonitorServer.ps1", validator, StringComparison.Ordinal);
        Assert.Contains("Install-Monitor.ps1", validator, StringComparison.Ordinal);
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
