using System.Text.Json;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProductionCandidateTests
{
    [Fact]
    public void PublishedBaselineSource_IsSingleNodeAndContainsNoDevelopmentAdminCredential()
    {
        var root = FindRepoRoot();
        var appSettingsPath = Path.Combine(root, "src", "Monitor.Web", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
        var json = document.RootElement;

        Assert.False(json.TryGetProperty("DevelopmentAdmin", out _));
        Assert.Equal("SingleNode", json.GetProperty("Deployment").GetProperty("Mode").GetString());
        Assert.Equal("Disabled", json.GetProperty("SharedState").GetProperty("Provider").GetString());
        Assert.False(json.GetProperty("HaState").GetProperty("UseSharedRegistrations").GetBoolean());
        Assert.False(json.GetProperty("HaState").GetProperty("UseSharedOperationalState").GetBoolean());
        Assert.False(json.GetProperty("Coordination").GetProperty("Enabled").GetBoolean());
        Assert.Equal("LocalFile", json.GetProperty("DataProtectionKeyStore").GetProperty("Mode").GetString());
    }

    [Fact]
    public void DevelopmentCredential_IsDevelopmentOnlyAndExcludedFromPublish()
    {
        var root = FindRepoRoot();
        var development = File.ReadAllText(Path.Combine(root, "src", "Monitor.Web", "appsettings.Development.json"));
        var project = File.ReadAllText(Path.Combine(root, "src", "Monitor.Web", "Monitor.Web.csproj"));

        Assert.Contains("DevelopmentAdmin", development, StringComparison.Ordinal);
        Assert.Contains("appsettings.Development.json", project, StringComparison.Ordinal);
        Assert.Contains("CopyToPublishDirectory=\"Never\"", project, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionExample_RemainsSecretFreeSingleNodeBaseline()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "deploy", "appsettings.Production.example.json");
        var text = File.ReadAllText(path);
        using var document = JsonDocument.Parse(text);
        var json = document.RootElement;

        Assert.Equal("SingleNode", json.GetProperty("Deployment").GetProperty("Mode").GetString());
        Assert.Equal("Disabled", json.GetProperty("SharedState").GetProperty("Provider").GetString());
        Assert.False(json.GetProperty("HaState").GetProperty("UseSharedRegistrations").GetBoolean());
        Assert.False(json.GetProperty("HaState").GetProperty("UseSharedOperationalState").GetBoolean());
        Assert.False(json.GetProperty("Coordination").GetProperty("Enabled").GetBoolean());
        Assert.Equal("LocalFile", json.GetProperty("DataProtectionKeyStore").GetProperty("Mode").GetString());
        Assert.False(json.TryGetProperty("ConnectionStrings", out _));
        Assert.DoesNotContain("Password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"ConnectionString\":", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DevelopmentAdmin", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CandidateWorkflow_RequiresReleaseTestsAuthenticationRestartSmokeAndChecksum()
    {
        var root = FindRepoRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "production-candidate.yml"));
        var authSmokePath = Path.Combine(root, "scripts", "Smoke-MonitorAuthentication.ps1");
        var authSmoke = File.ReadAllText(authSmokePath);

        Assert.Contains("workflow_call:", workflow, StringComparison.Ordinal);
        Assert.Contains("candidate_version:", workflow, StringComparison.Ordinal);
        Assert.Contains("Validate candidate version", workflow, StringComparison.Ordinal);
        Assert.Contains("windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("--warnaserror", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test Monitor.sln", workflow, StringComparison.Ordinal);
        Assert.Contains("Smoke-Monitor.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Smoke-MonitorAuthentication.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Rfc2898DeriveBytes", workflow, StringComparison.Ordinal);
        Assert.Contains("MONITOR_CANDIDATE_ADMIN_PASSWORD", workflow, StringComparison.Ordinal);
        Assert.Contains("Restart same published candidate", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-ProductionCandidate.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", workflow, StringComparison.Ordinal);
        Assert.Contains("SHA-256", workflow, StringComparison.Ordinal);
        Assert.Contains("upload-artifact@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("schemaVersion = 2", workflow, StringComparison.Ordinal);
        Assert.Contains("prerequisiteEvidence", workflow, StringComparison.Ordinal);
        Assert.Contains("p04 = @{", workflow, StringComparison.Ordinal);
        Assert.Contains("candidateVerification", workflow, StringComparison.Ordinal);
        Assert.Contains("sourceOfTruth = '#116'", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("realSqlAcceptance", workflow, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", authSmoke, StringComparison.Ordinal);
        Assert.Contains("/servers/connections", authSmoke, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Host $Password", authSmoke, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TaggedRelease_DelegatesToVerifiedProductionCandidateWorkflow()
    {
        var root = FindRepoRoot();
        var release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        Assert.Contains("uses: ./.github/workflows/production-candidate.yml", release, StringComparison.Ordinal);
        Assert.Contains("candidate_version: ${{ needs.resolve-version.outputs.version }}", release, StringComparison.Ordinal);
        Assert.Contains("^v[0-9]+\\.[0-9]+\\.[0-9]+", release, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet publish", release, StringComparison.Ordinal);
        Assert.DoesNotContain("Compress-Archive", release, StringComparison.Ordinal);
        Assert.DoesNotContain("zip -qr", release, StringComparison.Ordinal);
        Assert.DoesNotContain("upload-artifact@v4", release, StringComparison.Ordinal);
    }

    [Fact]
    public void TaggedRelease_PublishesOnlyExactVerifiedAssetsAndNeverClobbersExistingRelease()
    {
        var root = FindRepoRoot();
        var release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        Assert.Contains("publish-tagged-release:", release, StringComparison.Ordinal);
        Assert.Contains("github.event_name == 'push' && github.ref_type == 'tag'", release, StringComparison.Ordinal);
        Assert.Contains("contents: write", release, StringComparison.Ordinal);
        Assert.Contains("actions/download-artifact@v4", release, StringComparison.Ordinal);
        Assert.Contains("Download verified production package from this run", release, StringComparison.Ordinal);
        Assert.Contains("Verify downloaded product checksum", release, StringComparison.Ordinal);
        Assert.Contains("sha256sum", release, StringComparison.Ordinal);
        Assert.Contains("gh release view", release, StringComparison.Ordinal);
        Assert.Contains("gh release download", release, StringComparison.Ordinal);
        Assert.Contains("gh release create", release, StringComparison.Ordinal);
        Assert.Contains("--verify-tag", release, StringComparison.Ordinal);
        Assert.Contains("Existing release assets differ from the verified candidate; refusing mutation.", release, StringComparison.Ordinal);
        Assert.DoesNotContain("--clobber", release, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release upload", release, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate Monitor.sln for P0.5 acceptance tests.");
    }
}
