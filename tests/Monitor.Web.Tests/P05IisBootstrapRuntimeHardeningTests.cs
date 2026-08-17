using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05IisBootstrapRuntimeHardeningTests
{
    [Fact]
    public void Bootstrap_RejectsClientWindowsAndKeepsFreshHostPlanFromDereferencingMissingPool()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Bootstrap-IisProductionSingleNode.ps1");

        Assert.Contains("Get-CimInstance Win32_OperatingSystem", script, StringComparison.Ordinal);
        Assert.Contains("ProductType", script, StringComparison.Ordinal);
        Assert.Contains("requires Windows Server, not a Windows client workstation", script, StringComparison.Ordinal);
        Assert.Contains("$aclIdentity = $null", script, StringComparison.Ordinal);
        Assert.Contains("elseif (-not $Apply)", script, StringComparison.Ordinal);
        Assert.Contains("$aclIdentity = \"IIS AppPool\\$AppPoolName\"", script, StringComparison.Ordinal);
        Assert.Contains("Could not resolve the approved app-pool identity for ACL provisioning", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_RequiresDedicatedPoolAndExactSniSemantics()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Bootstrap-IisProductionSingleNode.ps1");

        Assert.Contains("$otherSites", script, StringComparison.Ordinal);
        Assert.Contains("is shared by other IIS site(s)", script, StringComparison.Ordinal);
        Assert.Contains("Monitor requires a dedicated app pool", script, StringComparison.Ordinal);
        Assert.Contains("sslFlags", script, StringComparison.Ordinal);
        Assert.Contains("exact SNI sslFlags=1", script, StringComparison.Ordinal);
        Assert.Contains("newly created HTTPS binding", script, StringComparison.Ordinal);
        Assert.Contains("Set-WebBinding", script, StringComparison.Ordinal);
        Assert.Contains("New-WebBinding", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_DoesNotRestartExistingSharedIisServicesWithoutExplicitOptIn()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Bootstrap-IisProductionSingleNode.ps1");

        Assert.Contains("[switch]$AllowIisServiceRestart", script, StringComparison.Ordinal);
        Assert.Contains("Restart-IisServicesAfterHostingBundle", script, StringComparison.Ordinal);
        Assert.Contains("will not silently restart shared IIS services", script, StringComparison.Ordinal);
        Assert.Contains("-AllowIisServiceRestart", script, StringComparison.Ordinal);
        Assert.Contains("net.exe stop was /y", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("net.exe start w3svc", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RestartNeeded", script, StringComparison.Ordinal);
        Assert.Contains("Reboot the server and rerun", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_PowerShell7PrerequisiteIsPinnedSignedAndSupportsOfflineMode()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Install-ProductionSingleNode.ps1");

        Assert.Contains("[ValidateSet('Online', 'Offline')]", script, StringComparison.Ordinal);
        Assert.Contains("[string]$PowerShellMode = 'Online'", script, StringComparison.Ordinal);
        Assert.Contains("PowerShellMsiInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("PowerShellMsiDownloadUrl", script, StringComparison.Ordinal);
        Assert.Contains("PowerShellMsiSha256", script, StringComparison.Ordinal);
        Assert.Contains("PowerShell-7.4.16-win-x64.msi", script, StringComparison.Ordinal);
        Assert.Contains("2c0c2036b0032375ad4f7809a92d0b6fa4a8e4ee89a75211514c4cf55ae22495", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.Contains("PowerShell 7 MSI must have a valid Microsoft Corporation Authenticode signature", script, StringComparison.Ordinal);
        Assert.Contains("if ($PowerShellMode -eq 'Offline')", script, StringComparison.Ordinal);
        Assert.Contains("Offline PowerShell 7 installation requires -PowerShellMsiInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-WebRequest -Uri $PowerShellMsiDownloadUrl.AbsoluteUri", script, StringComparison.Ordinal);
        Assert.Contains("Join-Path $env:ProgramFiles 'PowerShell\\7\\pwsh.exe'", script, StringComparison.Ordinal);
        Assert.Contains("No IIS mutation was attempted", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ForwardsOnlyExplicitIisRestartOptInIntoBootstrap()
    {
        var root = FindRepoRoot();
        var script = Read(root, "scripts/Install-ProductionSingleNode.ps1");

        Assert.Contains("[switch]$AllowIisServiceRestart", script, StringComparison.Ordinal);
        Assert.Contains("if ($AllowIisServiceRestart) { $bootstrapParameters.AllowIisServiceRestart = $true }", script, StringComparison.Ordinal);
        Assert.Contains("#162 durable RC publication + independent verification is complete", script, StringComparison.Ordinal);
        Assert.Contains("This switch does not itself satisfy or verify #162", script, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for P0.5 IIS bootstrap runtime hardening tests.");
    }
}
