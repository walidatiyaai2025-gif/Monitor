using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05PromotionProvenanceZipSafetyTests
{
    [Fact]
    public void PH_001_PromotionRequiresExactOuterArtifactDigestInput()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");
        var docs = Read("docs/P05_EXISTING_CANDIDATE_PROMOTION.md");

        Assert.Contains("expected_outer_artifact_digest: { required: true, type: string }", workflow, StringComparison.Ordinal);
        Assert.Contains("OUTER_DIGEST: ${{ inputs.expected_outer_artifact_digest }}", workflow, StringComparison.Ordinal);
        Assert.Contains("[[ \"${OUTER_DIGEST}\" =~ ^sha256:[a-f0-9]{64}$ ]]", workflow, StringComparison.Ordinal);
        Assert.Contains("sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void PH_002_ArtifactMetadataDigestMustMatchApprovedDigest()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");

        Assert.Contains("jq -r '.digest'", workflow, StringComparison.Ordinal);
        Assert.Contains("== \"${OUTER_DIGEST}\"", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PH_003_ArtifactMetadataHeadShaMustMatchApprovedSource()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");

        Assert.Contains("jq -r '.workflow_run.head_sha'", workflow, StringComparison.Ordinal);
        Assert.Contains("== \"${SOURCE_SHA}\"", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PH_004_SourceRunAndArtifactMustBelongToCurrentRepository()
    {
        var workflow = Read(".github/workflows/promote-existing-candidate.yml");

        Assert.Contains("REPOSITORY_ID: ${{ github.repository_id }}", workflow, StringComparison.Ordinal);
        Assert.Contains("jq -r '.repository.id'", workflow, StringComparison.Ordinal);
        Assert.Contains("jq -r '.head_repository.id'", workflow, StringComparison.Ordinal);
        Assert.Contains("jq -r '.workflow_run.repository_id'", workflow, StringComparison.Ordinal);
        Assert.Contains("jq -r '.workflow_run.head_repository_id'", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PH_005_ZipRejectsWindowsReservedDeviceNames()
    {
        var script = Read("scripts/Test-ExistingCandidatePromotion.ps1");

        Assert.Contains("CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]", script, StringComparison.Ordinal);
        Assert.Contains("Windows reserved device-name path segment", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PH_006_ZipRejectsTrailingDotsSpacesForbiddenAndControlCharacters()
    {
        var script = Read("scripts/Test-ExistingCandidatePromotion.ps1");

        Assert.Contains("EndsWith('.', [StringComparison]::Ordinal)", script, StringComparison.Ordinal);
        Assert.Contains("EndsWith(' ', [StringComparison]::Ordinal)", script, StringComparison.Ordinal);
        Assert.Contains("Windows-forbidden or control character", script, StringComparison.Ordinal);
        Assert.Contains("\x00-\x1F", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PH_007_ZipRejectsUnicodeCollisionsAndOverlongPaths()
    {
        var script = Read("scripts/Test-ExistingCandidatePromotion.ps1");

        Assert.Contains("Normalize([Text.NormalizationForm]::FormC)", script, StringComparison.Ordinal);
        Assert.Contains("[StringComparer]::OrdinalIgnoreCase", script, StringComparison.Ordinal);
        Assert.Contains("$canonicalEntryName.Length -gt 240", script, StringComparison.Ordinal);
        Assert.Contains("including Unicode normalization", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PH_008_ZipRejectsSymlinksAndReparsePoints()
    {
        var script = Read("scripts/Test-ExistingCandidatePromotion.ps1");

        Assert.Contains("$entry.ExternalAttributes", script, StringComparison.Ordinal);
        Assert.Contains("$unixFileType -eq 0xA000", script, StringComparison.Ordinal);
        Assert.Contains("[IO.FileAttributes]::ReparsePoint", script, StringComparison.Ordinal);
        Assert.Contains("symlink or reparse-point entry", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PH_009_ZipCapsEntryCountAndIndividualUncompressedSize()
    {
        var script = Read("scripts/Test-ExistingCandidatePromotion.ps1");

        Assert.Contains("$archive.Entries.Count -gt 4096", script, StringComparison.Ordinal);
        Assert.Contains("$entry.Length -gt 256MB", script, StringComparison.Ordinal);
        Assert.Contains("maximum is 4096", script, StringComparison.Ordinal);
        Assert.Contains("256 MiB uncompressed limit", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PH_010_ZipCapsTotalSizeAndCompressionRatio()
    {
        var script = Read("scripts/Test-ExistingCandidatePromotion.ps1");
        var ci = Read(".github/workflows/ci.yml");

        Assert.Contains("$totalUncompressedBytes -gt 1GB", script, StringComparison.Ordinal);
        Assert.Contains("$entry.Length -ge 1MB", script, StringComparison.Ordinal);
        Assert.Contains(") -gt 200.0", script, StringComparison.Ordinal);
        Assert.Contains("suspicious compression ratio above 200:1", script, StringComparison.Ordinal);
        Assert.Contains("Test-ExistingCandidatePromotionSafety.ps1", ci, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }

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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for P0.5 promotion provenance/ZIP safety tests.");
    }
}
