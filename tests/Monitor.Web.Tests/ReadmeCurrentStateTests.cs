using Xunit;

namespace Monitor.Web.Tests;

public sealed class ReadmeCurrentStateTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Readme_ReflectsCurrentBatchAndP05State()
    {
        var readme = Read("README.md");

        Assert.DoesNotContain("BATCH-100 is the active enterprise hardening program", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("B100-001..070", readme, StringComparison.Ordinal);
        Assert.Contains("BATCH-100 through BATCH-700 are complete", readme, StringComparison.Ordinal);
        Assert.Contains("660 completed hardening/UI task IDs", readme, StringComparison.Ordinal);
        Assert.Contains("P0.1 through P0.4 are COMPLETE", readme, StringComparison.Ordinal);
        Assert.Contains("P0.5 — First Production SingleNode", readme, StringComparison.Ordinal);
        Assert.Contains("#162 — durable RC.61 retention", readme, StringComparison.Ordinal);
        Assert.Contains("verify-durable-release", readme, StringComparison.Ordinal);
        Assert.Contains("#116 / #111 — real production acceptance", readme, StringComparison.Ordinal);
        Assert.Contains("does **not** substitute for actual production acceptance", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_PreservesCoreSafetyArchitecture()
    {
        var readme = Read("README.md");

        Assert.Contains("Monitoring GETs and health/observability GETs are cache/control-plane only", readme, StringComparison.Ordinal);
        Assert.Contains("Recommendations and Advisor output remain advisory-only", readme, StringComparison.Ordinal);
        Assert.Contains("SingleNode", readme, StringComparison.Ordinal);
        Assert.Contains("MultiNode stays fail-closed", readme, StringComparison.Ordinal);
        Assert.Contains("Browser/UI components never connect directly to monitored SQL Servers", readme, StringComparison.Ordinal);
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Root, relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
