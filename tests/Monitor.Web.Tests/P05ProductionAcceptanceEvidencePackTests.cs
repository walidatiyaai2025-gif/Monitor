using System.Text.Json;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05ProductionAcceptanceEvidencePackTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void EvidenceTemplate_HasExactlyFifteenFailClosedExternalGates()
    {
        using var document = JsonDocument.Parse(Read("deploy/production-acceptance-evidence.example.json"));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("SingleNode", root.GetProperty("environment").GetProperty("deploymentMode").GetString());

        var gates = root.GetProperty("gates").EnumerateObject().ToArray();
        Assert.Equal(15, gates.Length);
        Assert.All(gates, gate =>
        {
            Assert.False(gate.Value.GetProperty("passed").GetBoolean());
            Assert.Equal(JsonValueKind.Null, gate.Value.GetProperty("verifiedAtUtc").ValueKind);
            Assert.Equal(string.Empty, gate.Value.GetProperty("evidenceRef").GetString());
            Assert.Equal(string.Empty, gate.Value.GetProperty("evidenceSha256").GetString());
        });
    }

    [Fact]
    public void Generator_NeverMarksAProductionGatePass()
    {
        var text = Read("scripts/New-ProductionAcceptanceEvidencePack.ps1");
        Assert.Contains("passed = $false", text, StringComparison.Ordinal);
        Assert.DoesNotContain("passed = $true", text, StringComparison.Ordinal);
        Assert.Contains("The generator never marks a production gate PASS", text, StringComparison.Ordinal);
        Assert.Contains("Monitor-$CandidateVersion-win-x64.zip", text, StringComparison.Ordinal);
        Assert.Contains("absolute Windows path", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-loopback DNS host name", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_RequiresExactGateSetAndEveryGatePass()
    {
        var text = Read("scripts/Test-ProductionAcceptanceEvidence.ps1");
        Assert.Contains("Assert-ExactProperties -Value $record.gates -Allowed $requiredGates", text, StringComparison.Ordinal);
        Assert.Contains("Required production gate '$gateName' is not PASS", text, StringComparison.Ordinal);
        Assert.Contains("requiredGateCount = $requiredGates.Count", text, StringComparison.Ordinal);
        Assert.Contains("15/15", Read("docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_BindsEachGateToRelativeEvidenceAndMatchingSha256()
    {
        var text = Read("scripts/Test-ProductionAcceptanceEvidence.ps1");
        Assert.Contains("[IO.Path]::IsPathRooted($evidenceRef)", text, StringComparison.Ordinal);
        Assert.Contains("escapes EvidenceRoot", text, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $targetFull -Algorithm SHA256", text, StringComparison.Ordinal);
        Assert.Contains("Evidence SHA-256 mismatch", text, StringComparison.Ordinal);
        Assert.Contains("evidenceSha256", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_RejectsSecretConnectionStringProviderErrorAndSqlTextMaterial()
    {
        var text = Read("scripts/Test-ProductionAcceptanceEvidence.ps1");
        Assert.Contains("secret-like key", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("connection-string", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SqlException", text, StringComparison.Ordinal);
        Assert.Contains("Login failed for user", text, StringComparison.Ordinal);
        Assert.Contains("select|insert|update|delete|drop|alter|create", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_ProducesClosureSummaryOnlyAfterAllValidation()
    {
        var text = Read("scripts/Test-ProductionAcceptanceEvidence.ps1");
        var gateLoop = text.IndexOf("foreach ($gateName in $requiredGates)", StringComparison.Ordinal);
        var summary = text.LastIndexOf("$summary = [ordered]@{", StringComparison.Ordinal);
        Assert.True(gateLoop >= 0 && summary > gateLoop);
        Assert.Contains("result = 'PASS'", text, StringComparison.Ordinal);
        Assert.Contains("evidencePackSha256", text, StringComparison.Ordinal);
        Assert.Contains("acceptedAtUtc cannot be earlier", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsCandidate_ParsesExecutesAndBundlesEvidenceTooling()
    {
        var text = Read(".github/workflows/production-candidate.yml");
        Assert.Contains("scripts/New-ProductionAcceptanceEvidencePack.ps1", text, StringComparison.Ordinal);
        Assert.Contains("scripts/Test-ProductionAcceptanceEvidence.ps1", text, StringComparison.Ordinal);
        Assert.Contains("Exercise acceptance evidence closure validator", text, StringComparison.Ordinal);
        Assert.Contains("production-acceptance-evidence.example.json", text, StringComparison.Ordinal);
        Assert.Contains("negative gate unexpectedly passed", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tampered evidence hash unexpectedly passed", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret-bearing evidence unexpectedly passed", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AcceptanceRunbook_KeepsExternalIisAsTheOnlyClosureAuthority()
    {
        var text = Read("docs/PRODUCTION_SINGLENODE_ACCEPTANCE.md");
        Assert.Contains("External acceptance evidence pack", text, StringComparison.Ordinal);
        Assert.Contains("New-ProductionAcceptanceEvidencePack.ps1", text, StringComparison.Ordinal);
        Assert.Contains("Test-ProductionAcceptanceEvidence.ps1", text, StringComparison.Ordinal);
        Assert.Contains("does not perform IIS deployment", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#116", text, StringComparison.Ordinal);
        Assert.Contains("must remain OPEN", text, StringComparison.OrdinalIgnoreCase);
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
