using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800AdvancedEvidenceAccessibilityAcceptanceTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void TempDbEvidence_ExposesCompleteAccessibleTableStructure()
    {
        var view = Read("src/Monitor.Web/Views/Shared/_TempDbDiagnostics.cshtml");

        Assert.Contains("class=\"responsive-table\" role=\"table\" aria-label=\"TempDB diagnostics by server\" tabindex=\"0\"", view, StringComparison.Ordinal);
        Assert.Equal(7, Count(view, "role=\"columnheader\""));
        Assert.True(Count(view, "role=\"cell\"") >= 7);
        Assert.Contains("<strong role=\"cell\">@server.Name</strong>", view, StringComparison.Ordinal);
        Assert.Contains("<span role=\"cell\"><partial name=\"_HealthSourceBadge\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"responsive-table\" role=\"region\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void TransactionLogAndHaEvidence_ExposeCompleteAccessibleTableStructure()
    {
        var view = Read("src/Monitor.Web/Views/Shared/_TransactionLogHaDiagnostics.cshtml");

        Assert.Equal(2, Count(view, "class=\"responsive-table\" role=\"table\""));
        Assert.Equal(14, Count(view, "role=\"columnheader\""));
        Assert.True(Count(view, "role=\"cell\"") >= 14);
        Assert.Contains("aria-label=\"@server.Name transaction log evidence\" tabindex=\"0\"", view, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"@server.Name HA database replica evidence\" tabindex=\"0\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"responsive-table\" role=\"region\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponsiveEvidenceTables_RetainHorizontalScrollAndKeyboardFocusVisibility()
    {
        var css = Read("src/Monitor.Web/wwwroot/css/portal.css");

        Assert.Contains(".responsive-table {", css, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", css, StringComparison.Ordinal);
        Assert.Contains("overscroll-behavior-inline: contain", css, StringComparison.Ordinal);
        Assert.Contains("-webkit-overflow-scrolling: touch", css, StringComparison.Ordinal);
        Assert.Contains(".responsive-table [role=\"row\"] { min-width: 680px; }", css, StringComparison.Ordinal);
        Assert.Contains(".responsive-table:focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("outline: 2px solid currentColor", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 520px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessibilityHardening_DoesNotChangeAdvancedEvidenceTruthOrCollectionBoundary()
    {
        var tempDb = Read("src/Monitor.Web/Views/Shared/_TempDbDiagnostics.cshtml");
        var logHa = Read("src/Monitor.Web/Views/Shared/_TransactionLogHaDiagnostics.cshtml");
        var combined = string.Join('\n', tempDb, logHa);

        Assert.Contains("Not collected", combined, StringComparison.Ordinal);
        Assert.Contains("not evaluated", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unavailable", combined, StringComparison.Ordinal);
        Assert.Contains("AdvancedEvidenceProjection.BuildTempDb", tempDb, StringComparison.Ordinal);
        Assert.Contains("AdvancedEvidenceProjection.BuildTransactionLogs", logHa, StringComparison.Ordinal);
        Assert.Contains("AdvancedEvidenceProjection.BuildHa", logHa, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SnapshotQuery", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("method=\"post\"", combined, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
