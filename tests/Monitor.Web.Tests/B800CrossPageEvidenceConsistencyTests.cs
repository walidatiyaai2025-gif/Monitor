using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800CrossPageEvidenceConsistencyTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void ServerDetails_AndDedicatedModules_SurfaceTheSameCachedEvidenceContracts()
    {
        var serverDetails = Read("src/Monitor.Web/Views/Operations/ServerDetails.cshtml");

        AssertEvidenceContract(
            serverDetails,
            "DATABASE AVAILABILITY",
            "databases.Restoring",
            "databases.Recovering",
            "databases.RecoveryPending",
            "databases.Suspect",
            "databases.Emergency",
            "databases.OfflineOrOther");
        AssertEvidenceContract(
            Read("src/Monitor.Web/Views/Operations/DatabaseHealth.cshtml"),
            "DATABASE STATE BY SERVER",
            "detail.Restoring",
            "detail.Recovering",
            "detail.RecoveryPending",
            "detail.Suspect",
            "detail.Emergency",
            "detail.OfflineOrOther");

        AssertEvidenceContract(
            serverDetails,
            "BACKUP EVIDENCE",
            "backups.BackedUpLast24Hours",
            "backups.MissingFullBackupLast24Hours",
            "backups.LastFullBackupAtUtc");
        AssertEvidenceContract(
            Read("src/Monitor.Web/Views/Operations/Backups.cshtml"),
            "BACKUP COVERAGE BY SERVER",
            "backup.BackedUpLast24Hours",
            "backup.MissingFullBackupLast24Hours",
            "backup?.LastFullBackupAtUtc");

        AssertEvidenceContract(
            serverDetails,
            "SQL AGENT",
            "jobs.TotalJobs",
            "jobs.EnabledJobs",
            "jobs.FailedLastRun");
        AssertEvidenceContract(
            Read("src/Monitor.Web/Views/Operations/Jobs.cshtml"),
            "SQL AGENT BY SERVER",
            "jobs.TotalJobs",
            "jobs.EnabledJobs",
            "jobs.FailedLastRun");

        AssertEvidenceContract(
            serverDetails,
            "STORAGE ALLOCATION",
            "storage.TotalAllocatedBytes",
            "storage.DataAllocatedBytes",
            "storage.LogAllocatedBytes");
        AssertEvidenceContract(
            Read("src/Monitor.Web/Views/Operations/Storage.cshtml"),
            "ALLOCATION BY SERVER",
            "storage.TotalAllocatedBytes",
            "storage.DataAllocatedBytes",
            "storage.LogAllocatedBytes");
    }

    [Fact]
    public void DedicatedHealthModules_ProvideSafeCachedDrillDownsToServerEvidence()
    {
        foreach (var relative in new[]
        {
            "src/Monitor.Web/Views/Operations/DatabaseHealth.cshtml",
            "src/Monitor.Web/Views/Operations/Backups.cshtml",
            "src/Monitor.Web/Views/Operations/Jobs.cshtml",
            "src/Monitor.Web/Views/Operations/Storage.cshtml"
        })
        {
            var view = Read(relative);

            Assert.Contains("asp-action=\"ServerDetails\"", view, StringComparison.Ordinal);
            Assert.Contains("asp-route-id=\"@server.Id\"", view, StringComparison.Ordinal);
            Assert.Contains("_HealthSourceBadge", view, StringComparison.Ordinal);
            Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SELECT ", view, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CrossPageEvidence_UsesExplicitUnavailableLanguageInsteadOfSyntheticHealth()
    {
        var views = new[]
        {
            Read("src/Monitor.Web/Views/Operations/ServerDetails.cshtml"),
            Read("src/Monitor.Web/Views/Operations/DatabaseHealth.cshtml"),
            Read("src/Monitor.Web/Views/Operations/Backups.cshtml"),
            Read("src/Monitor.Web/Views/Operations/Jobs.cshtml"),
            Read("src/Monitor.Web/Views/Operations/Storage.cshtml")
        };

        foreach (var view in views)
        {
            Assert.Contains("Not collected", view, StringComparison.Ordinal);
            Assert.DoesNotContain("assume healthy", view, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertEvidenceContract(string source, string heading, params string[] tokens)
    {
        Assert.Contains(heading, source, StringComparison.Ordinal);
        foreach (var token in tokens)
            Assert.Contains(token, source, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
