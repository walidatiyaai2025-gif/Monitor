using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProductionAcceptanceFinalizationTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Finalizer_RequiresExplicitHumanAcknowledgementAndSafeOperatorIdentity()
    {
        var text = Read("scripts/Complete-ProductionAcceptance.ps1");
        Assert.Contains("[switch]$AcknowledgeFinalAcceptance", text, StringComparison.Ordinal);
        Assert.Contains("requires explicit -AcknowledgeFinalAcceptance", text, StringComparison.Ordinal);
        Assert.Contains("AcceptedBy must be a non-placeholder bounded single-line operator identity", text, StringComparison.Ordinal);
        Assert.Contains("SqlException", text, StringComparison.Ordinal);
        Assert.Contains("secret-like", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Finalizer_OnlySetsFinalAcceptanceMetadataAndNeverMarksAGatePass()
    {
        var text = Read("scripts/Complete-ProductionAcceptance.ps1");
        Assert.Contains("$record.acceptedBy = $AcceptedBy", text, StringComparison.Ordinal);
        Assert.Contains("$record.acceptedAtUtc = $acceptedAtUtc", text, StringComparison.Ordinal);
        Assert.DoesNotContain(".passed = $true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-ProductionAcceptanceGate.ps1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Finalizer_ProspectivelyValidatesBeforeAuthoritativeCommitAndDetectsConcurrentMutation()
    {
        var text = Read("scripts/Complete-ProductionAcceptance.ps1");
        var prospectiveValidation = text.IndexOf("-EvidencePath $prospectivePath", StringComparison.Ordinal);
        var concurrencyCheck = text.IndexOf("Evidence pack changed during finalization", StringComparison.Ordinal);
        var authoritativeMove = text.IndexOf("Move-Item -LiteralPath $prospectivePath -Destination $resolvedPackPath -Force", StringComparison.Ordinal);

        Assert.True(prospectiveValidation >= 0);
        Assert.True(concurrencyCheck > prospectiveValidation);
        Assert.True(authoritativeMove > concurrencyCheck);
        Assert.Contains("Get-FileHash -LiteralPath $resolvedPackPath -Algorithm SHA256", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Finalizer_FailsClosedAndRestoresUnacceptedPackWhenFinalValidationFails()
    {
        var text = Read("scripts/Complete-ProductionAcceptance.ps1");
        Assert.Contains("Write-AtomicText -Path $resolvedPackPath -Text $originalRaw", text, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $closureSummaryPath", text, StringComparison.Ordinal);
        Assert.Contains("Test-ProductionAcceptanceEvidence.ps1", text, StringComparison.Ordinal);
        Assert.Contains("original unaccepted pack", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Finalizer_RejectsUnsafeSummaryPathsAndAlreadyAcceptedPacks()
    {
        var text = Read("scripts/Complete-ProductionAcceptance.ps1");
        Assert.Contains("[IO.Path]::IsPathRooted($RelativePath)", text, StringComparison.Ordinal);
        Assert.Contains("ClosureSummaryFile escapes the evidence-pack root", text, StringComparison.Ordinal);
        Assert.Contains("ClosureSummaryFile must not overwrite the evidence pack", text, StringComparison.Ordinal);
        Assert.Contains("Closure summary already exists", text, StringComparison.Ordinal);
        Assert.Contains("already contains final operator acceptance metadata and is immutable", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsCandidate_ParsesExecutesAndBundlesFinalizerWithNegativeCases()
    {
        var text = Read(".github/workflows/production-candidate.yml");
        Assert.Contains("scripts/Complete-ProductionAcceptance.ps1", text, StringComparison.Ordinal);
        Assert.Contains("Exercise final operator acceptance finalizer", text, StringComparison.Ordinal);
        Assert.Contains("finalizer without acknowledgement unexpectedly passed", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("finalizer before all gates unexpectedly passed", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unsafe finalizer summary path unexpectedly passed", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("second finalization unexpectedly passed", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runbook_UsesFinalizerAndKeepsGitHubClosureSeparateFromEvidenceFinalization()
    {
        var text = Read("docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md");
        Assert.Contains("Complete-ProductionAcceptance.ps1", text, StringComparison.Ordinal);
        Assert.Contains("AcknowledgeFinalAcceptance", text, StringComparison.Ordinal);
        Assert.Contains("prospective", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#116", text, StringComparison.Ordinal);
        Assert.Contains("must remain OPEN", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Finalizer_HasNoDeploymentSqlOrIssueClosingSideEffects()
    {
        var text = Read("scripts/Complete-ProductionAcceptance.ps1");
        Assert.DoesNotContain("Restart-WebAppPool", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Set-ItemProperty IIS:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-Sqlcmd", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh issue close", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api.github.com", text, StringComparison.OrdinalIgnoreCase);
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
