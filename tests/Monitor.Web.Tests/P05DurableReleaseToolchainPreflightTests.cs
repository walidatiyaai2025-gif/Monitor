using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05DurableReleaseToolchainPreflightTests
{
    private const string PreflightPath = "scripts/Verify-DurableReleaseToolchain.sh";
    private const string HarnessPath = "scripts/Test-DurableReleaseToolchainSafety.sh";
    private const string ReleaseWorkflowPath = ".github/workflows/release.yml";
    private const string PromotionWorkflowPath = ".github/workflows/promote-existing-candidate.yml";
    private const string IndependentWorkflowPath = ".github/workflows/verify-durable-release.yml";
    private const string CiWorkflowPath = ".github/workflows/ci.yml";

    [Fact]
    public void TC_001_AllDurableVerifierCallSitesGateReleaseVerificationOnCapabilityPass()
    {
        var preflight = Read(PreflightPath);
        var release = Read(ReleaseWorkflowPath);
        var promotion = Read(PromotionWorkflowPath);
        var independent = Read(IndependentWorkflowPath);

        foreach (var command in new[] { "gh", "jq", "realpath", "stat", "mktemp", "find", "sort", "mv", "sha256sum", "awk", "dirname", "basename", "chmod", "rm", "rmdir", "cat" })
        {
            Assert.Contains(command, preflight, StringComparison.Ordinal);
        }

        var releasePreflight = release.IndexOf("bash \"${preflight}\"", StringComparison.Ordinal);
        var releaseLookup = release.IndexOf("releases/tags/${RELEASE_TAG}", StringComparison.Ordinal);
        Assert.True(releasePreflight >= 0 && releaseLookup > releasePreflight, "Tagged release must capability-check before release/tag verification API access.");

        var promotionPreflight = promotion.IndexOf("bash scripts/Verify-DurableReleaseToolchain.sh", StringComparison.Ordinal);
        var promotionRunLookup = promotion.IndexOf("actions/runs/${RUN_ID}", StringComparison.Ordinal);
        Assert.True(promotionPreflight >= 0 && promotionRunLookup > promotionPreflight, "Promotion must capability-check before its GitHub metadata API work.");

        var independentPreflight = independent.IndexOf("bash scripts/Verify-DurableReleaseToolchain.sh", StringComparison.Ordinal);
        var independentVerifier = independent.IndexOf("bash scripts/Verify-DurableRelease.sh", StringComparison.Ordinal);
        Assert.True(independentPreflight >= 0 && independentVerifier > independentPreflight, "Independent verification must capability-check before invoking the shared verifier.");
    }

    [Fact]
    public void TC_002_PreflightFunctionallyProbesJqRatherThanOnlyCheckingPresence()
    {
        var preflight = Read(PreflightPath);

        Assert.Contains("command -v \"$required\"", preflight, StringComparison.Ordinal);
        Assert.Contains("jq -n -r '\"durable-release-toolchain-ok\"'", preflight, StringComparison.Ordinal);
        Assert.Contains("jq functional probe failed", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void TC_003_PreflightRequiresRealpathExistingCanonicalSemantics()
    {
        var preflight = Read(PreflightPath);

        Assert.Contains("realpath -e -- \"$probe_root\"", preflight, StringComparison.Ordinal);
        Assert.Contains("did not preserve an already-canonical existing path", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void TC_004_PreflightProvesAllSecurityCriticalStatFormats()
    {
        var preflight = Read(PreflightPath);

        Assert.Contains("stat -Lc '%d:%i'", preflight, StringComparison.Ordinal);
        Assert.Contains("stat -Lc '%a'", preflight, StringComparison.Ordinal);
        Assert.Contains("stat -Lc '%h'", preflight, StringComparison.Ordinal);
        Assert.Contains("stat -c%s", preflight, StringComparison.Ordinal);
        Assert.Contains("stat device/inode format is incompatible", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void TC_005_PreflightProvesPrivateMktempDirectChildSemantics()
    {
        var preflight = Read(PreflightPath);

        Assert.Contains("mktemp -d -p \"$probe_root\" '.monitor-toolchain.XXXXXXXXXX'", preflight, StringComparison.Ordinal);
        Assert.Contains("$(dirname -- \"$mktemp_child\")", preflight, StringComparison.Ordinal);
        Assert.Contains("did not create a private 0700 directory", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void TC_006_PreflightProvesDeterministicFindPrintfEnumeration()
    {
        var preflight = Read(PreflightPath);

        Assert.Contains("find \"$probe_root/find-probe\" -mindepth 1 -maxdepth 1 -printf '%f\\n' | sort", preflight, StringComparison.Ordinal);
        Assert.Contains("find -printf plus sort probe is incompatible", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void TC_007_PreflightProvesCanonicalSha256sumAwkOutput()
    {
        var preflight = Read(PreflightPath);

        Assert.Contains("sha256sum \"$probe_root/hash-file\" | awk '{print $1}'", preflight, StringComparison.Ordinal);
        Assert.Contains("^[a-f0-9]{64}$", preflight, StringComparison.Ordinal);
        Assert.Contains("one canonical lowercase SHA-256 digest", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void TC_008_PreflightProvesNoClobberFileRenameAndCollisionSemantics()
    {
        var preflight = Read(PreflightPath);

        Assert.Contains("mv -T --no-clobber -- \"$probe_root/mv-file-source\" \"$probe_root/mv-file-destination\"", preflight, StringComparison.Ordinal);
        Assert.Contains("mv file finalization did not preserve source identity", preflight, StringComparison.Ordinal);
        Assert.Contains("consumed a file source when the destination existed", preflight, StringComparison.Ordinal);
        Assert.Contains("overwrote an existing file destination", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void TC_009_PreflightProvesNoClobberDirectoryPublicationAndCollisionSemantics()
    {
        var preflight = Read(PreflightPath);

        Assert.Contains("mv -T --no-clobber -- \"$probe_root/mv-dir-source\" \"$probe_root/mv-dir-destination\"", preflight, StringComparison.Ordinal);
        Assert.Contains("mv directory publication did not preserve source identity", preflight, StringComparison.Ordinal);
        Assert.Contains("consumed a directory source when the destination existed", preflight, StringComparison.Ordinal);
        Assert.Contains("overwrote an existing directory destination", preflight, StringComparison.Ordinal);
        Assert.Contains("merged source content into the destination", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void TC_010_CiRunsPositiveAndBrokenMvFailFastSafetyCoverage()
    {
        var harness = Read(HarnessPath);
        var ci = Read(CiWorkflowPath);
        var release = Read(ReleaseWorkflowPath);

        Assert.Contains("$work/bad-bin/mv", harness, StringComparison.Ordinal);
        Assert.Contains("-T|--no-clobber|--", harness, StringComparison.Ordinal);
        Assert.Contains("GH_CALL_LOG", harness, StringComparison.Ordinal);
        Assert.Contains("[[ ! -e \"$work/bad-gh.log\" ]]", harness, StringComparison.Ordinal);
        Assert.Contains("find \"$work/tmp\" -mindepth 1 -maxdepth 1 -print -quit", harness, StringComparison.Ordinal);
        Assert.Contains("Durable release toolchain positive and fail-fast no-clobber drift checks passed.", harness, StringComparison.Ordinal);

        Assert.Contains("bash -n scripts/Verify-DurableReleaseToolchain.sh", ci, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/Test-DurableReleaseToolchainSafety.sh", ci, StringComparison.Ordinal);
        Assert.Contains("bash scripts/Verify-DurableReleaseToolchain.sh", ci, StringComparison.Ordinal);
        Assert.Contains("bash scripts/Test-DurableReleaseToolchainSafety.sh", ci, StringComparison.Ordinal);

        Assert.Contains("contents/scripts/Verify-DurableReleaseToolchain.sh?ref=${GITHUB_SHA}", release, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/checkout@", release, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for durable-release toolchain preflight tests.");
    }
}
