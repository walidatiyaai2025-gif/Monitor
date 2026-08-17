using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProductionAcceptanceSessionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Initializer_RequiresFreshAbsoluteSessionRootAndNeverReusesAWorkspace()
    {
        var text = Read("scripts/New-ProductionAcceptanceSession.ps1");
        Assert.Contains("$Name must be an absolute Windows path", text, StringComparison.Ordinal);
        Assert.Contains("must not contain path traversal segments", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SessionRoot must be fresh and must not already exist", text, StringComparison.Ordinal);
        Assert.Contains("SessionRoot must not be a drive or UNC share root", text, StringComparison.Ordinal);
        Assert.Contains("SessionRoot parent directory must already exist", text, StringComparison.Ordinal);
        Assert.Contains("Move-Item -LiteralPath $tempRoot -Destination $resolvedSessionRoot", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Initializer_BindsExactCandidateChecksumSelectedHashAndReadableZipBeforeSessionCreation()
    {
        var text = Read("scripts/New-ProductionAcceptanceSession.ps1");
        Assert.Contains("Monitor-$CandidateVersion-win-x64.zip", text, StringComparison.Ordinal);
        Assert.Contains("Candidate checksum file name must be exactly", text, StringComparison.Ordinal);
        Assert.Contains("ExpectedProductSha256", text, StringComparison.Ordinal);
        Assert.Contains("ValidatePattern('^[a-fA-F0-9]{64}$')", text, StringComparison.Ordinal);
        Assert.Contains("$selectedProductHash = $ExpectedProductSha256.ToLowerInvariant()", text, StringComparison.Ordinal);
        Assert.Contains("Candidate checksum SHA-256 does not match the selected product SHA-256", text, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $ArtifactPath -Algorithm SHA256", text, StringComparison.Ordinal);
        Assert.Contains("Candidate artifact SHA-256 does not match the selected checksum file", text, StringComparison.Ordinal);
        Assert.Contains("Candidate artifact SHA-256 does not match the selected product SHA-256", text, StringComparison.Ordinal);
        Assert.Contains("[System.IO.Compression.ZipFile]::OpenRead", text, StringComparison.Ordinal);
        Assert.Contains("Candidate ZIP must contain at least one entry", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Initializer_BindsAcceptanceControlToolingCommitIntoLockedManifest()
    {
        var text = Read("scripts/New-ProductionAcceptanceSession.ps1");
        Assert.Contains("[string]$OperatorToolingCommit", text, StringComparison.Ordinal);
        Assert.Contains("$normalizedToolingCommit = $OperatorToolingCommit.ToLowerInvariant()", text, StringComparison.Ordinal);
        Assert.Contains("operatorToolingCommit = $normalizedToolingCommit", text, StringComparison.Ordinal);
        Assert.Contains("OperatorToolingCommit = $normalizedToolingCommit", text, StringComparison.Ordinal);
        Assert.Contains("ExpectedOperatorToolkitManifestSha256", text, StringComparison.Ordinal);
        Assert.Contains("operatorToolkitManifestSha256 = $expectedToolkitManifestHash", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Initializer_RechecksSelectedHashAfterCopyAndBindsManifestAndEvidencePack()
    {
        var text = Read("scripts/New-ProductionAcceptanceSession.ps1");
        Assert.Contains("Copied candidate artifact SHA-256 does not match the selected product SHA-256", text, StringComparison.Ordinal);
        Assert.Contains("Copied checksum no longer matches the selected product SHA-256", text, StringComparison.Ordinal);
        Assert.Contains("-ArtifactSha256 $selectedProductHash", text, StringComparison.Ordinal);
        Assert.Contains("artifactSha256 = $selectedProductHash", text, StringComparison.Ordinal);
        Assert.Contains("selectedProductSha256 = $selectedProductHash", text, StringComparison.Ordinal);
        Assert.Contains("SelectedProductSha256 = $selectedProductHash", text, StringComparison.Ordinal);
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
    public void WindowsCandidate_ParsesExecutesAndBundlesSessionInitializerWithSelectedHashNegativeCases()
    {
        var workflow = Read(".github/workflows/production-candidate.yml");
        var runtime = Read("scripts/Test-ProductionAcceptanceSession.ps1");
        Assert.Contains("scripts/New-ProductionAcceptanceSession.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/Test-ProductionAcceptanceSession.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Exercise immutable production acceptance session initializer", workflow, StringComparison.Ordinal);
        Assert.Contains("$env:MONITOR_PACKAGED_TOOLKIT_ROOT/New-ProductionAcceptanceSession.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Export-ProductionAcceptanceToolkit.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-ProductionAcceptanceToolkit.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("ExpectedProductSha256 = $hash", runtime, StringComparison.Ordinal);
        Assert.Contains("OperatorToolingCommit = $toolingCommit", runtime, StringComparison.Ordinal);
        Assert.Contains("ExpectedOperatorToolkitManifestSha256 = $toolkit.ToolkitManifestSha256", runtime, StringComparison.Ordinal);
        Assert.Contains("reused session root unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tampered checksum unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Substituted ZIP and checksum pair unexpectedly passed selected-hash binding", runtime, StringComparison.Ordinal);
        Assert.Contains("non-zip artifact unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wrong independently supplied Acceptance Control Toolkit manifest SHA-256 unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret-like session metadata unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("traversal-bearing absolute session root unexpectedly passed", runtime, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runbook_StartsWithSidecarToolingAndSelectedHashBoundCandidateSession()
    {
        var text = Read("docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md");
        Assert.Contains("Acceptance Control Toolkit", text, StringComparison.Ordinal);
        Assert.Contains("New-ProductionAcceptanceSession.ps1", text, StringComparison.Ordinal);
        Assert.Contains("-ExpectedProductSha256", text, StringComparison.Ordinal);
        Assert.Contains("-OperatorToolingCommit", text, StringComparison.Ordinal);
        Assert.Contains("-ExpectedOperatorToolkitManifestSha256", text, StringComparison.Ordinal);
        Assert.Contains("d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5", text, StringComparison.Ordinal);
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
