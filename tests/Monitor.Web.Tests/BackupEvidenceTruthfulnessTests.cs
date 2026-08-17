using Xunit;

namespace Monitor.Web.Tests;

public sealed class BackupEvidenceTruthfulnessTests
{
    [Fact]
    public void BackupView_DoesNotPresentIncompleteEvidenceAsHealthyOrCompliant()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Views/Operations/Backups.cshtml"));

        Assert.Contains("missing > 0 ? \"critical\" : unavailable > 0 ? \"warning\" : \"healthy\"", view, StringComparison.Ordinal);
        Assert.Contains("latestFull == default || unavailable > 0 ? \"warning\" : \"healthy\"", view, StringComparison.Ordinal);
        Assert.Contains("server(s) also lack backup evidence", view, StringComparison.Ordinal);
        Assert.Contains("does not claim B300 RPO compliance", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Batch300BackupCompliance", view, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", view, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
