using Xunit;

namespace Monitor.Web.Tests;

public sealed class DbaCommandCenterAcceptanceTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void DbaCommandCenter_ExposesExplicitVisibleAdministrationSurface()
    {
        var controller = Read("src/Monitor.Web/Controllers/DbaController.cs");
        var view = Read("src/Monitor.Web/Views/Dba/Index.cshtml");
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");

        Assert.Contains("[HttpGet(\"/dba\")]", controller, StringComparison.Ordinal);
        Assert.Contains("Run DBA inspection", view, StringComparison.Ordinal);
        Assert.Contains("Refresh base snapshot", view, StringComparison.Ordinal);
        Assert.Contains("Backup plan - all databases", view, StringComparison.Ordinal);
        Assert.Contains("DBA recommendations and fix queries", view, StringComparison.Ordinal);
        Assert.Contains("DBA to-do list", view, StringComparison.Ordinal);
        Assert.Contains("DBA Command Center", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("display:none", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeepInspection_CoversQueryCpuMemoryDatabaseIoBlockingAndIndexEvidence()
    {
        var service = Read("src/Monitor.Web/Services/DbaCommandCenter.cs");
        var view = Read("src/Monitor.Web/Views/Dba/Index.cshtml");

        Assert.Contains("DbaQueryHotspot", service, StringComparison.Ordinal);
        Assert.Contains("TotalCpuMs", service, StringComparison.Ordinal);
        Assert.Contains("LogicalReads", service, StringComparison.Ordinal);
        Assert.Contains("LogicalWrites", service, StringComparison.Ordinal);
        Assert.Contains("DbaMemoryGrant", service, StringComparison.Ordinal);
        Assert.Contains("BufferPoolMb", service, StringComparison.Ordinal);
        Assert.Contains("sys.dm_os_wait_stats", service, StringComparison.Ordinal);
        Assert.Contains("sys.dm_io_virtual_file_stats", service, StringComparison.Ordinal);
        Assert.Contains("blocking_session_id", service, StringComparison.Ordinal);
        Assert.Contains("DbaMissingIndexCandidate", service, StringComparison.Ordinal);
        Assert.Contains("Top query consumers", view, StringComparison.Ordinal);
        Assert.Contains("Active memory grants", view, StringComparison.Ordinal);
        Assert.Contains("Per-database resources", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Recommendations_AreEvidenceBasedReviewOnlyAndProvideValidationSql()
    {
        var service = Read("src/Monitor.Web/Services/DbaCommandCenter.cs");
        var view = Read("src/Monitor.Web/Views/Dba/Index.cshtml");

        Assert.Contains("Evidence", service, StringComparison.Ordinal);
        Assert.Contains("Recommendation", service, StringComparison.Ordinal);
        Assert.Contains("FixSql", service, StringComparison.Ordinal);
        Assert.Contains("ValidationSql", service, StringComparison.Ordinal);
        Assert.Contains("REVIEW ONLY - NEVER AUTO-EXECUTE", service, StringComparison.Ordinal);
        Assert.Contains("Fix / investigation SQL", view, StringComparison.Ordinal);
        Assert.Contains("Validation SQL", view, StringComparison.Ordinal);
        Assert.Contains("never executed by Monitor", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueryText_IsBoundedAndLiteralRedactedBeforeRetention()
    {
        var service = Read("src/Monitor.Web/Services/DbaCommandCenter.cs");

        Assert.Contains("SanitizeQueryText", service, StringComparison.Ordinal);
        Assert.Contains("StringLiteral.Replace", service, StringComparison.Ordinal);
        Assert.Contains("NumericLiteral.Replace", service, StringComparison.Ordinal);
        Assert.Contains("value.Length <= 600", service, StringComparison.Ordinal);
    }

    [Fact]
    public void BackupManagement_CanTargetOneDatabaseOrAllAndIncludesRestoreValidation()
    {
        var controller = Read("src/Monitor.Web/Controllers/DbaController.cs");
        var formatter = Read("src/Monitor.Web/Services/DbaBackupPlanFormatter.cs");
        var view = Read("src/Monitor.Web/Views/Dba/Index.cshtml");

        Assert.Contains("string? database = null", controller, StringComparison.Ordinal);
        Assert.Contains("Backup plan - all databases", view, StringComparison.Ordinal);
        Assert.Contains("asp-route-database", view, StringComparison.Ordinal);
        Assert.Contains("BACKUP DATABASE", formatter, StringComparison.Ordinal);
        Assert.Contains("RESTORE VERIFYONLY", formatter, StringComparison.Ordinal);
        Assert.Contains("CHECKSUM", formatter, StringComparison.Ordinal);
        Assert.Contains("DIFFERENTIAL", formatter, StringComparison.Ordinal);
        Assert.Contains("BACKUP LOG", formatter, StringComparison.Ordinal);
        Assert.Contains("DBCC CHECKDB", formatter, StringComparison.Ordinal);
    }

    [Fact]
    public void FlutterDashboard_IsReadOnlyAndNeverReceivesSqlCredentials()
    {
        var app = Read("apps/monitor_dba_flutter/lib/main.dart");
        var readme = Read("apps/monitor_dba_flutter/README.md");
        var controller = Read("src/Monitor.Web/Controllers/DbaController.cs");

        Assert.Contains("/api/dba/summary", app, StringComparison.Ordinal);
        Assert.Contains("DBA Dashboard", app, StringComparison.Ordinal);
        Assert.Contains("Refresh", app, StringComparison.Ordinal);
        Assert.Contains("todos", app, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never connects to SQL Server", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlConnection", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", app, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[HttpGet(\"/api/dba/summary\")]", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstInstallAndAdminUpgrade_AreVisibleWizardDrivenAndRollbackAware()
    {
        var installer = Read("deploy/installer/Install-MonitorWizard.ps1");
        var upgrader = Read("deploy/upgrade/Apply-MonitorUpgrade.ps1");
        var upgradeView = Read("src/Monitor.Web/Views/AdminUpgrade/Index.cshtml");
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");

        Assert.Contains("Next", installer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Back", installer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Install", installer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("health/ready", installer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Monitor Upgrades", upgradeView, StringComparison.Ordinal);
        Assert.Contains("Monitor Upgrades", layout, StringComparison.Ordinal);
        Assert.Contains("SHA-256", upgradeView, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stop-Service", upgrader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Start-Service", upgrader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rollback", upgrader, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
