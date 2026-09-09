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
    public void ExistingTaggedRelease_FailsClosedOnReleaseNoteDriftWithoutRewriting()
    {
        var workflow = Read(".github/workflows/release.yml");

        Assert.Contains("existing_release_json=\"\"", workflow, StringComparison.Ordinal);
        Assert.Contains("observed_notes=\"$(jq -r '.body // empty'", workflow, StringComparison.Ordinal);
        Assert.Contains("if [[ \"${observed_notes}\" != \"${expected_notes}\" ]]", workflow, StringComparison.Ordinal);
        Assert.Contains("Existing GitHub Release notes do not match the deterministic artifact-bound release identity; refusing mutation.", workflow, StringComparison.Ordinal);
        Assert.Contains("Existing GitHub Release already matches the verified product ZIP/checksum and deterministic notes; leaving it immutable.", workflow, StringComparison.Ordinal);

        var notesIndex = workflow.IndexOf("expected_notes=\"$(cat \"${notes_file}\")\"", StringComparison.Ordinal);
        var existingReadIndex = workflow.IndexOf("existing_release_json=\"\"", StringComparison.Ordinal);
        var existingVerifyIndex = workflow.IndexOf("verify_release_assets \"${RUNNER_TEMP}/existing-release\"", StringComparison.Ordinal);
        var createIndex = workflow.IndexOf("gh release create", StringComparison.Ordinal);

        Assert.True(notesIndex >= 0 && existingReadIndex > notesIndex,
            "Existing release metadata must be checked against the same deterministic notes assembled for creation.");
        Assert.True(existingVerifyIndex > existingReadIndex && createIndex > existingVerifyIndex,
            "An existing release must be verified and returned without reaching the create path.");
    }

    [Fact]
    public void TaggedRelease_CreatePathRequiresProvenNotFoundRatherThanAmbiguousApiFailure()
    {
        var workflow = Read(".github/workflows/release.yml");

        Assert.Contains("release_probe_error=\"${RUNNER_TEMP}/release-probe.err\"", workflow, StringComparison.Ordinal);
        Assert.Contains("release_probe_exit=$?", workflow, StringComparison.Ordinal);
        Assert.Contains("if [[ \"${release_probe_exit}\" -eq 0 ]]", workflow, StringComparison.Ordinal);
        Assert.Contains("if ! grep -Eqi '(HTTP 404|Not Found)' \"${release_probe_error}\"; then", workflow, StringComparison.Ordinal);
        Assert.Contains("Could not establish that the tagged GitHub Release is absent; refusing publication on ambiguous API state.", workflow, StringComparison.Ordinal);

        var probeIndex = workflow.IndexOf("release_probe_error=\"${RUNNER_TEMP}/release-probe.err\"", StringComparison.Ordinal);
        var absenceIndex = workflow.IndexOf("if ! grep -Eqi '(HTTP 404|Not Found)'", StringComparison.Ordinal);
        var createIndex = workflow.IndexOf("gh release create", StringComparison.Ordinal);
        Assert.True(probeIndex >= 0 && absenceIndex > probeIndex && createIndex > absenceIndex,
            "Release creation must be reachable only after the release API has proven an actual not-found result.");
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
