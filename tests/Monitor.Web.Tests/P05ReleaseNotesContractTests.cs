using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ReleaseNotesContractTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void TaggedRelease_PublishesDeterministicArtifactBoundReleaseNotes()
    {
        var workflow = Read(".github/workflows/release.yml");

        Assert.Contains("uses: ./.github/workflows/production-candidate.yml", workflow, StringComparison.Ordinal);
        Assert.Contains("notes_file=\"${RUNNER_TEMP}/release-notes.md\"", workflow, StringComparison.Ordinal);
        Assert.Contains("## Monitor ${RELEASE_VERSION}", workflow, StringComparison.Ordinal);
        Assert.Contains("- Tag: ${RELEASE_TAG}", workflow, StringComparison.Ordinal);
        Assert.Contains("- Release source commit: ${GITHUB_SHA}", workflow, StringComparison.Ordinal);
        Assert.Contains("- Windows x64 package: ${ZIP_NAME}", workflow, StringComparison.Ordinal);
        Assert.Contains("- Product SHA-256: ${PRODUCT_SHA256}", workflow, StringComparison.Ordinal);
        Assert.Contains("- Companion checksum: ${CHECKSUM_NAME}", workflow, StringComparison.Ordinal);
        Assert.Contains("Package provenance: verified production-candidate.yml output from this exact tagged workflow run.", workflow, StringComparison.Ordinal);
        Assert.Contains("Publication is artifact retention; it does not deploy IIS/SQL or prove external production acceptance.", workflow, StringComparison.Ordinal);
        Assert.Contains("--notes-file \"${notes_file}\"", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--generate-notes", workflow, StringComparison.Ordinal);

        var checksumIndex = workflow.IndexOf("actual_hash=\"$(sha256sum", StringComparison.Ordinal);
        var notesIndex = workflow.IndexOf("notes_file=\"${RUNNER_TEMP}/release-notes.md\"", StringComparison.Ordinal);
        var createIndex = workflow.IndexOf("gh release create", StringComparison.Ordinal);
        var verifyIndex = workflow.IndexOf("verify_release_assets \"${RUNNER_TEMP}/created-release\"", StringComparison.Ordinal);

        Assert.True(checksumIndex >= 0 && notesIndex > checksumIndex,
            "Release notes must be assembled only after the package checksum has been verified.");
        Assert.True(createIndex > notesIndex,
            "The immutable GitHub Release must consume the deterministic notes file assembled from verified release identity.");
        Assert.True(verifyIndex > createIndex,
            "Release publication must still be followed by independent durable-release verification.");
    }

    [Fact]
    public void TaggedRelease_DoesNotRebuildOrRepackageDuringPublication()
    {
        var workflow = Read(".github/workflows/release.yml");
        var publicationStart = workflow.IndexOf("publish-tagged-release:", StringComparison.Ordinal);
        Assert.True(publicationStart >= 0);
        var publication = workflow[publicationStart..];

        Assert.DoesNotContain("dotnet publish", publication, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Compress-Archive", publication, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("zip -", publication, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Download verified production package from this run", publication, StringComparison.Ordinal);
        Assert.Contains("Verified production artifact must contain exactly the product ZIP and companion checksum.", publication, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
