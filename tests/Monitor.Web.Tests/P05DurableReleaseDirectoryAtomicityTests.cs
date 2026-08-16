using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05DurableReleaseDirectoryAtomicityTests
{
    private const string VerifierPath = "scripts/Verify-DurableRelease.sh";
    private const string HarnessPath = "scripts/Test-DurableReleaseVerifierSafety.sh";
    private const string CiWorkflow = ".github/workflows/ci.yml";

    [Fact]
    public void AT_001_DestinationRemainsAbsentDuringNetworkVerification()
    {
        var verifier = Read(VerifierPath);
        var harness = Read(HarnessPath);

        Assert.True(verifier.Split("destination appeared before durable-release verification completed", StringSplitOptions.None).Length - 1 >= 3);
        Assert.Contains("FAKE_GH_EXPECT_DEST", harness, StringComparison.Ordinal);
        Assert.Contains("destination became visible before verification finished", harness, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_002_RandomPrivateHiddenStagingDirectoryIsCreatedUnderTrustedRoot()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("mktemp -d -p \"$trusted_root_canonical\" '.monitor-durable-release.XXXXXXXXXX'", verifier, StringComparison.Ordinal);
        Assert.Contains("chmod 700 -- \"$staging_dir\"", verifier, StringComparison.Ordinal);
        Assert.Contains("verifier-owned staging directory permissions must be 0700", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_003_TrustedRootIdentityIsPinnedAcrossCriticalPhases()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("trusted_root_identity=", verifier, StringComparison.Ordinal);
        Assert.Contains("assert_trusted_root_identity()", verifier, StringComparison.Ordinal);
        Assert.Contains("trusted root identity changed during verification", verifier, StringComparison.Ordinal);
        Assert.True(verifier.Split("assert_trusted_root_identity", StringSplitOptions.None).Length - 1 >= 6);
    }

    [Fact]
    public void AT_004_StagingIdentityAndContainmentArePinned()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("staging_identity=", verifier, StringComparison.Ordinal);
        Assert.Contains("verifier-owned staging directory was reparented during verification", verifier, StringComparison.Ordinal);
        Assert.Contains("verifier-owned staging directory identity changed during verification", verifier, StringComparison.Ordinal);
        Assert.Contains("verifier-owned staging directory permissions changed during verification", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_005_FailureAndSignalCleanupIsInstalledBeforeDownloads()
    {
        var verifier = Read(VerifierPath);
        var trapIndex = verifier.IndexOf("trap cleanup_workspace EXIT", StringComparison.Ordinal);
        var downloadIndex = verifier.IndexOf("releases/assets/${first_zip_id}", StringComparison.Ordinal);

        Assert.True(trapIndex >= 0 && downloadIndex > trapIndex, "Cleanup trap must be armed before network downloads.");
        Assert.Contains("trap 'exit 129' HUP", verifier, StringComparison.Ordinal);
        Assert.Contains("trap 'exit 130' INT", verifier, StringComparison.Ordinal);
        Assert.Contains("trap 'exit 143' TERM", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_006_CleanupNeverUsesRecursiveDeletion()
    {
        var verifier = Read(VerifierPath);

        Assert.DoesNotContain("rm -rf", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("rm -r ", verifier, StringComparison.Ordinal);
        Assert.Contains("cleanup_owned_path", verifier, StringComparison.Ordinal);
        Assert.Contains("rmdir -- \"$path\"", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_007_FinalNamesAreCompletedInsideStagingAfterSecondSnapshot()
    {
        var verifier = Read(VerifierPath);
        var secondSnapshot = verifier.IndexOf("second_json=\"$(snapshot_release)\"", StringComparison.Ordinal);
        var zipFinalize = verifier.IndexOf("mv -T --no-clobber -- \"$zip_tmp\" \"$zip_path\"", StringComparison.Ordinal);
        var directoryPublish = verifier.IndexOf("mv -T --no-clobber -- \"$staging_dir\" \"$destination\"", StringComparison.Ordinal);

        Assert.True(secondSnapshot >= 0 && zipFinalize > secondSnapshot && directoryPublish > zipFinalize);
        Assert.Contains("final staging payload must contain exactly the ZIP and checksum", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_008_EntireVerifiedDirectoryIsPublishedWithOneNoClobberRename()
    {
        var verifier = Read(VerifierPath);

        Assert.Contains("mv -T --no-clobber -- \"$staging_dir\" \"$destination\"", verifier, StringComparison.Ordinal);
        Assert.Contains("atomic directory publication encountered an unexpected destination collision", verifier, StringComparison.Ordinal);
        Assert.Contains("published destination identity differs from verified staging", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_009_CleanupIsDisarmedOnlyAfterPublishedPayloadReverification()
    {
        var verifier = Read(VerifierPath);
        var publishedVerify = verifier.IndexOf("published durable-release payload must contain exactly the ZIP and checksum", StringComparison.Ordinal);
        var disarm = verifier.IndexOf("cleanup_armed=false", StringComparison.Ordinal);

        Assert.True(publishedVerify >= 0 && disarm > publishedVerify, "Cleanup may be disarmed only after published payload revalidation.");
        Assert.Contains("published ZIP bytes changed during atomic directory publication", verifier, StringComparison.Ordinal);
        Assert.Contains("trap - EXIT HUP INT TERM", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_010_RuntimeHarnessCoversCleanupCollisionAndCiExecution()
    {
        var harness = Read(HarnessPath);
        var ci = Read(CiWorkflow);

        Assert.Contains("assert_no_hidden_staging", harness, StringComparison.Ordinal);
        Assert.Contains("Late destination collision unexpectedly passed", harness, StringComparison.Ordinal);
        Assert.Contains("collision-sentinel", harness, StringComparison.Ordinal);
        Assert.Contains("[[ ! -e \"$mutated\" && ! -L \"$mutated\" ]]", harness, StringComparison.Ordinal);
        Assert.Contains("synthetic positive, cleanup, collision and TOCTOU checks passed", harness, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/Verify-DurableRelease.sh", ci, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for durable-release directory atomicity tests.");
    }
}
