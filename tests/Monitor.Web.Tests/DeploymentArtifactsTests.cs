using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Monitor.Web.Tests;

public sealed partial class DeploymentArtifactsTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ProductionTemplate_IsValidJson_AndContainsNoCredentialMaterial()
    {
        var text = Read("deploy/appsettings.Production.example.json");
        using var json = JsonDocument.Parse(text);

        Assert.Equal("SingleNode", json.RootElement.GetProperty("Deployment").GetProperty("Mode").GetString());
        Assert.Equal("MONITOR_SHARED_STATE_SQL_CONNECTION", json.RootElement.GetProperty("SharedState").GetProperty("ConnectionStringEnvironmentVariable").GetString());
        Assert.DoesNotContain("Password=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User ID=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HashBase64", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SaltBase64", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsServiceHosting_IsEnabledWithoutExternalWrapper()
    {
        var project = Read("src/Monitor.Web/Monitor.Web.csproj");
        var program = Read("src/Monitor.Web/Program.cs");

        Assert.Contains("Microsoft.Extensions.Hosting.WindowsServices", project, StringComparison.Ordinal);
        Assert.Contains("UseWindowsService", program, StringComparison.Ordinal);
        Assert.Contains("ServiceName = \"Monitor\"", program, StringComparison.Ordinal);
    }

    [Fact]
    public void DeploymentGuides_ArePresent_AndReferenceReadinessSmokeGate()
    {
        foreach (var path in new[] { "docs/DEPLOY_IIS.md", "docs/DEPLOY_WINDOWS_SERVICE.md", "docs/DEPLOY_REVERSE_PROXY.md" })
        {
            var text = Read(path);
            Assert.Contains("/health/ready", text, StringComparison.Ordinal);
            Assert.Contains("Smoke-Monitor.ps1", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void StateDatabaseRuntimeRole_IsNarrow()
    {
        var script = StripSqlComments(Read("scripts/sql/monitor_state_least_privilege.sql"));

        Assert.Contains("GRANT SELECT ON dbo.MonitorSharedStateSchema", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GRANT SELECT, INSERT, UPDATE ON dbo.MonitorSharedStateDocuments", script, StringComparison.OrdinalIgnoreCase);
        AssertNoHighPrivilegeGrant(script);
        Assert.DoesNotMatch(new Regex(@"\bGRANT\s+DELETE\b", RegexOptions.IgnoreCase), script);
    }

    [Fact]
    public void MonitoredSqlRole_MatchesCollectorReadSurface()
    {
        var script = StripSqlComments(Read("scripts/sql/monitored_sql_least_privilege.sql"));

        Assert.Contains("VIEW SERVER PERFORMANCE STATE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VIEW SERVER STATE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VIEW ANY DATABASE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GRANT SELECT ON sys.master_files", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GRANT SELECT ON dbo.backupset", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GRANT SELECT ON dbo.sysjobs", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GRANT SELECT ON dbo.sysjobservers", script, StringComparison.OrdinalIgnoreCase);
        AssertNoHighPrivilegeGrant(script);
        Assert.DoesNotContain("db_owner", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpgradeChecklist_RequiresBackup_Readiness_AndRollbackPoint()
    {
        var text = Read("docs/UPGRADE_CHECKLIST.md");

        Assert.Contains("operational backup", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("previous", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/health/ready", text, StringComparison.Ordinal);
        Assert.Contains("ROLLBACK_RUNBOOK.md", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseWorkflow_BuildsAndTestsBeforePackaging()
    {
        var text = Read(".github/workflows/release.yml");
        var build = text.IndexOf("dotnet build", StringComparison.Ordinal);
        var test = text.IndexOf("dotnet test", StringComparison.Ordinal);
        var publish = text.IndexOf("dotnet publish", StringComparison.Ordinal);

        Assert.True(build >= 0 && test > build && publish > test);
        Assert.Contains("--warnaserror", text, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v4", text, StringComparison.Ordinal);
        Assert.Contains("contents: read", text, StringComparison.Ordinal);
        Assert.Contains("sha256sum", text, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScript_UsesOnlyControlPlaneHealthEndpoints_AndRequiresHttps()
    {
        var text = Read("scripts/Smoke-Monitor.ps1");

        Assert.Contains("'/health/live'", text, StringComparison.Ordinal);
        Assert.Contains("'/health/ready'", text, StringComparison.Ordinal);
        Assert.Contains("'/health'", text, StringComparison.Ordinal);
        Assert.Contains("require HTTPS", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/servers", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection-lab", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-connection", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RollbackRunbook_FailsClosedAroundKeysStateAndSchema()
    {
        var text = Read("docs/ROLLBACK_RUNBOOK.md");

        Assert.Contains("Never delete Data Protection keys", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never run `monitor_shared_state_v1.sql`", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Smoke-Monitor.ps1", text, StringComparison.Ordinal);
        Assert.Contains("/health/ready", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DeploymentArtifacts_DoNotContainRealisticSecretCanary()
    {
        const string canary = "super-secret-canary-20260811";
        var paths = new[]
        {
            "deploy/appsettings.Production.example.json",
            "docs/DEPLOY_IIS.md",
            "docs/DEPLOY_WINDOWS_SERVICE.md",
            "docs/DEPLOY_REVERSE_PROXY.md",
            "scripts/sql/monitor_state_least_privilege.sql",
            "scripts/sql/monitored_sql_least_privilege.sql",
            "docs/UPGRADE_CHECKLIST.md",
            ".github/workflows/release.yml",
            "scripts/Smoke-Monitor.ps1",
            "docs/ROLLBACK_RUNBOOK.md"
        };

        Assert.All(paths, path => Assert.DoesNotContain(canary, Read(path), StringComparison.Ordinal));
    }

    private static void AssertNoHighPrivilegeGrant(string sql)
    {
        Assert.DoesNotMatch(HighPrivilegeGrantRegex(), sql);
    }

    [GeneratedRegex(@"\bGRANT\s+(CONTROL|ALTER\s+ANY|IMPERSONATE|UNSAFE|BACKUP|CREATE\s+ANY|TAKE\s+OWNERSHIP)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighPrivilegeGrantRegex();

    private static string StripSqlComments(string sql) =>
        Regex.Replace(sql, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
