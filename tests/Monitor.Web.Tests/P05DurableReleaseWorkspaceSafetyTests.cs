using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05DurableReleaseWorkspaceSafetyTests
{
    private const string VerifierPath = "scripts/Verify-DurableRelease.sh";
    private const string HarnessPath = "scripts/Test-DurableReleaseVerifierSafety.sh";
    private const string ReleaseWorkflow = ".github/workflows/release.yml";
    private const string PromotionWorkflow = ".github/workflows/promote-existing-candidate.yml";
    private const string CiWorkflow = ".github/workflows/ci.yml";

    [Fact]
    public void WS_001_VersionGrammarRejectsUnsafeSeparatorForms()
    {
        var verifier = Read(VerifierPath);
        var release = Read(ReleaseWorkflow);
        var promotion = Read(PromotionWorkflow);
        var harness = Read(HarnessPath);

        Assert.Contains("([.-][0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?", verifier, StringComparison.Ordinal);
        Assert.Contains("([.-][0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?", release, StringComparison.Ordinal);
        Assert.Contains("([.-][0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?", promotion, StringComparison.Ordinal);
        Assert.Contains("1.2.3-rc..1", harness, StringComparison.Ordinal);
        Assert.Contains("version format is invalid", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void WS_002_VerifierRequiresExistingAbsoluteNonSymlinkTrustedRoot()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("--trusted-root", verifier, StringComparison.Ordinal);
        Assert.Contains("trusted root is required", verifier, StringComparison.Ordinal);
        Assert.Contains("trusted root must be an absolute path", verifier, StringComparison.Ordinal);
        Assert.Contains("trusted root must not be the filesystem root", verifier, StringComparison.Ordinal);
        Assert.Contains("trusted root must be an existing non-symlink directory", verifier, StringComparison.Ordinal);
        Assert.Contains("realpath -e -- \"$trusted_root\"", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void WS_003_DestinationMustBeCanonicalDirectChildOfTrustedRoot()
    {
        var verifier = Read(VerifierPath);
        var harness = Read(HarnessPath);

        Assert.Contains("destination must be an absolute path", verifier, StringComparison.Ordinal);
        Assert.Contains("destination_parent_canonical=\"$(realpath -e -- \"$destination_parent\")\"", verifier, StringComparison.Ordinal);
        Assert.Contains("destination must be a direct child of the trusted root", verifier, StringComparison.Ordinal);
        Assert.Contains("destination must be canonical and contained by the trusted root", verifier, StringComparison.Ordinal);
        Assert.Contains("traversal_destination=", harness, StringComparison.Ordinal);
    }

    [Fact]
    public void WS_004_VerifierNeverDeletesCallerControlledDestination()
    {
        var verifier = Read(VerifierPath);
        var harness = Read(HarnessPath);

        Assert.Contains("destination must not already exist", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("rm -rf -- \"$destination\"", verifier, StringComparison.Ordinal);
        Assert.Contains("sentinel.txt", harness, StringComparison.Ordinal);
        Assert.Contains("Existing-destination case unexpectedly passed", harness, StringComparison.Ordinal);
    }

    [Fact]
    public void WS_005_VerifierCreatesPrivateOwnedWorkspaceBeforeDownloads()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("umask 077", verifier, StringComparison.Ordinal);
        Assert.Contains("mktemp -d -p \"$trusted_root_canonical\"", verifier, StringComparison.Ordinal);
        Assert.Contains("verifier-owned staging directory permissions must be 0700", verifier, StringComparison.Ordinal);
        Assert.True(
            verifier.IndexOf("mktemp -d -p", StringComparison.Ordinal) < verifier.IndexOf("releases/assets/${first_zip_id}", StringComparison.Ordinal),
            "Private staging creation must precede exact-ID asset downloads.");
    }

    [Fact]
    public void WS_006_WorkspaceIdentityIsPinnedAcrossNetworkVerification()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("trusted_root_identity=\"$(stat -Lc '%d:%i' \"$trusted_root_canonical\")\"", verifier, StringComparison.Ordinal);
        Assert.Contains("staging_identity=\"$(stat -Lc '%d:%i' \"$staging_dir\")\"", verifier, StringComparison.Ordinal);
        Assert.Contains("assert_staging_identity()", verifier, StringComparison.Ordinal);
        Assert.Contains("verifier-owned staging directory identity changed during verification", verifier, StringComparison.Ordinal);
        Assert.True(verifier.Split("assert_staging_identity", StringSplitOptions.None).Length - 1 >= 5);
    }

    [Fact]
    public void WS_007_ExactIdDownloadsUseHiddenNoClobberTemporaryFiles()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("zip_tmp=\"${staging_dir}/${zip_tmp_name}\"", verifier, StringComparison.Ordinal);
        Assert.Contains("checksum_tmp=\"${staging_dir}/${checksum_tmp_name}\"", verifier, StringComparison.Ordinal);
        Assert.Contains("set -o noclobber", verifier, StringComparison.Ordinal);
        Assert.Contains("releases/assets/${first_zip_id}\" >\"$zip_tmp\"", verifier, StringComparison.Ordinal);
        Assert.Contains("releases/assets/${first_checksum_id}\" >\"$checksum_tmp\"", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void WS_008_DownloadedTemporaryFilesMustBePrivateRegularSingleLinkFiles()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("downloaded asset must be a regular non-symlink file", verifier, StringComparison.Ordinal);
        Assert.Contains("downloaded asset must have exactly one hard link", verifier, StringComparison.Ordinal);
        Assert.Contains("downloaded asset permissions must be 0600", verifier, StringComparison.Ordinal);
        Assert.Contains("stat -c%s \"$zip_tmp\"", verifier, StringComparison.Ordinal);
        Assert.Contains("sha256sum \"$checksum_tmp\"", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void WS_009_FinalNamesAppearOnlyAfterSecondSnapshotPasses()
    {
        var verifier = Read(VerifierPath);
        var secondSnapshot = verifier.IndexOf("second_json=\"$(snapshot_release)\"", StringComparison.Ordinal);
        var firstPublish = verifier.IndexOf("mv -T --no-clobber -- \"$zip_tmp\" \"$zip_path\"", StringComparison.Ordinal);

        Assert.True(secondSnapshot >= 0 && firstPublish > secondSnapshot, "Final staged names must follow the unchanged second REST snapshot.");
        Assert.Contains("final durable-release output names must not pre-exist in staging", verifier, StringComparison.Ordinal);
        Assert.Contains("ZIP finalization encountered an unexpected name collision", verifier, StringComparison.Ordinal);
        Assert.Contains("checksum finalization encountered an unexpected name collision", verifier, StringComparison.Ordinal);
        Assert.Contains("final staging payload must contain exactly the ZIP and checksum", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void WS_010_CallersAndRuntimeHarnessOwnWorkspaceSafetyContract()
    {
        var release = Read(ReleaseWorkflow);
        var promotion = Read(PromotionWorkflow);
        var harness = Read(HarnessPath);
        var ci = Read(CiWorkflow);

        Assert.Contains("--trusted-root \"${RUNNER_TEMP}\"", release, StringComparison.Ordinal);
        Assert.Contains("--trusted-root \"${RUNNER_TEMP}\"", promotion, StringComparison.Ordinal);
        Assert.Contains("Symlink-destination case unexpectedly passed", harness, StringComparison.Ordinal);
        Assert.Contains("Traversal-destination case unexpectedly passed", harness, StringComparison.Ordinal);
        Assert.Contains("[[ ! -e \"$mutated\" && ! -L \"$mutated\" ]]", harness, StringComparison.Ordinal);
        Assert.Contains("bash scripts/Test-DurableReleaseVerifierSafety.sh", ci, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for durable release workspace-safety tests.");
    }
}
