using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProductionAcceptanceSessionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Initializer_RequiresFreshAbsoluteSessionRootAndNeverReusesAWorkspace()
    {
        var text = Read("scripts/New-ProductionAcceptanceSession.ps1");
        Assert.Contains("SessionRoot must be an absolute Windows path", text, StringComparison.Ordinal);
        Assert.Contains("SessionRoot must be fresh and must not already exist", text, StringComparison.Ordinal);
        Assert.Contains("SessionRoot must not be a drive or UNC share root", text, StringComparison.Ordinal);
        Assert.Contains("SessionRoot parent directory must already exist", text, StringComparison.Ordinal);
        Assert.Contains("Move-Item -LiteralPath $tempRoot -Destination $resolvedSessionRoot", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Initializer_BindsExactCandidateChecksumAndReadableZipBeforeSessionCreation()
    {
        var text = Read("scripts/New-ProductionAcceptanceSession.ps1");
        Assert.Contains("Monitor-$CandidateVersion-win-x64.zip", text, StringComparison.Ordinal);
        Assert.Contains("Candidate checksum file name must be exactly", text, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $ArtifactPath -Algorithm SHA256", text, StringComparison.Ordinal);
        Assert.Contains("Candidate artifact SHA-256 does not match", text, StringComparison.Ordinal);
        Assert.Contains("[System.IO.Compression.ZipFile]::OpenRead", text, StringComparison.Ordinal);
        Assert.Contains("Candidate ZIP must contain at least one entry", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Initializer_UsesCanonicalPackGeneratorAndProvesZeroOfFifteenExternalGates()
    {
        var text = Read("scripts/New-ProductionAcceptanceSession.ps1");
        Assert.Contains("New-ProductionAcceptanceEvidencePack.ps1", text, StringComparison.Ordinal);
        Assert.Contains("$gateProperties.Count -ne 15", text, StringComparison.Ordinal);
        Assert.Contains("ExternalGateCount = 15", text, StringComparison.Ordinal);
        Assert.Contains("ExternalGatesPassed = 0", text, StringComparison.Ordinal);
        Assert.Contains("ProductionAccepted = $false", text, StringComparison.Ordinal);
        Assert.DoesNotContain(".passed = $true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("acceptedBy =", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("acceptedAtUtc =", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Initializer_CopiesCandidateAndCreatesLockedManifestAndOperatorNextSteps()
    {
        var text = Read("scripts/New-ProductionAcceptanceSession.ps1");
        Assert.Contains("Copy-Item -LiteralPath $ArtifactPath", text, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $ChecksumPath", text, StringComparison.Ordinal);
        Assert.Contains("session-manifest.json", text, StringComparison.Ordinal);
        Assert.Contains("session-manifest.sha256", text, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256", text, StringComparison.Ordinal);
        Assert.Contains("PreparedFailClosed", text, StringComparison.Ordinal);
        Assert.Contains("OPERATOR-NEXT-STEPS.txt", text, StringComparison.Ordinal);
        Assert.Contains("Set-ProductionAcceptanceGate.ps1", text, StringComparison.Ordinal);
        Assert.Contains("Complete-ProductionAcceptance.ps1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Initializer_RejectsSecretProviderErrorConnectionStringAndSqlTextMetadata()
    {
        var text = Read("scripts/New-ProductionAcceptanceSession.ps1");
        Assert.Contains("secret-like", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("connection-string", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SqlException", text, StringComparison.Ordinal);
        Assert.Contains("Login failed for user", text, StringComparison.Ordinal);
        Assert.Contains("select|insert|update|delete|drop|alter|create", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsCandidate_ParsesExecutesAndBundlesSessionInitializerWithNegativeCases()
    {
        var workflow = Read(".github/workflows/production-candidate.yml");
        var runtime = Read("scripts/Test-ProductionAcceptanceSession.ps1");
        Assert.Contains("scripts/New-ProductionAcceptanceSession.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/Test-ProductionAcceptanceSession.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Exercise immutable production acceptance session initializer", workflow, StringComparison.Ordinal);
        Assert.Contains("Copy-Item scripts/New-ProductionAcceptanceSession.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("reused session root unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tampered checksum unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-zip artifact unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret-like session metadata unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runbook_StartsWithCandidateBoundSessionAndKeepsExternalAcceptanceSeparate()
    {
        var text = Read("docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md");
        Assert.Contains("New-ProductionAcceptanceSession.ps1", text, StringComparison.Ordinal);
        Assert.Contains("PreparedFailClosed", text, StringComparison.Ordinal);
        Assert.Contains("0/15", text, StringComparison.Ordinal);
        Assert.Contains("#116", text, StringComparison.Ordinal);
        Assert.Contains("must remain OPEN", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Initializer_HasNoIisSqlGatePassFinalizationOrGitHubSideEffects()
    {
        var text = Read("scripts/New-ProductionAcceptanceSession.ps1");
        Assert.DoesNotContain("Restart-WebAppPool", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Set-ItemProperty IIS:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-Sqlcmd", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh issue close", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api.github.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-AcknowledgeFinalAcceptance", text, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root containing Monitor.sln was not found.");
    }
}
