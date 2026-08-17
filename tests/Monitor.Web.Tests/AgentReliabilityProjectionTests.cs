using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class AgentReliabilityProjectionTests
{
    [Fact]
    public void Build_ScoresBoundedRunHistoryWithoutInventingScheduleLateness()
    {
        var jobs = new SqlAgentHealthSnapshot(
            4,
            4,
            1,
            [
                new AgentJobRunSnapshot("Nightly ETL", "DOMAIN\\sqlagent", false, 20260817030000, 120),
                new AgentJobRunSnapshot("Nightly ETL", "DOMAIN\\sqlagent", false, 20260816030000, 60),
                new AgentJobRunSnapshot("Nightly ETL", "DOMAIN\\sqlagent", true, 20260815030000, 60)
            ]);

        var result = AgentReliabilityProjection.Build(jobs);

        var job = Assert.Single(result);
        Assert.Equal("Nightly ETL", job.JobKey);
        Assert.Equal("DOMAIN\\sqlagent", job.Owner);
        Assert.Equal(33.33d, job.SuccessRatePercent);
        Assert.Equal(2, job.FailureStreak);
        Assert.Equal(120, job.P95Duration.TotalSeconds);
        Assert.Equal(100d, job.DurationRegressionPercent);
        Assert.Equal(46.67d, job.Score);
        Assert.Equal(B400Severity.Warning, job.Severity);
        Assert.True(job.AlertWorthy);
        Assert.False(job.ScheduleLatenessEvaluated);
    }

    [Fact]
    public void Build_MissingOrMalformedHistoryDoesNotInventReliableState()
    {
        Assert.Empty(AgentReliabilityProjection.Build(new SqlAgentHealthSnapshot(2, 2, 0)));
        Assert.Empty(AgentReliabilityProjection.Build(new SqlAgentHealthSnapshot(
            2,
            2,
            0,
            [new AgentJobRunSnapshot("Job", "owner", true, 20261399000000, 1)])));
    }

    [Fact]
    public void JobsView_WiresHistoryReliabilityAndKeepsCommandsOut()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Views/Operations/Jobs.cshtml"));
        var collector = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Services/SqlServerSnapshotCollector.cs"));

        Assert.Contains("AgentReliabilityProjection.Build", view, StringComparison.Ordinal);
        Assert.Contains("B400 AGENT RELIABILITY", view, StringComparison.Ordinal);
        Assert.Contains("Schedule lateness is", view, StringComparison.Ordinal);
        Assert.Contains("not evaluated", view, StringComparison.Ordinal);
        Assert.Contains("msdb.dbo.sysjobhistory", collector, StringComparison.Ordinal);
        Assert.DoesNotContain("sysjobsteps", collector, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command_text", collector, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlConnection", view, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
