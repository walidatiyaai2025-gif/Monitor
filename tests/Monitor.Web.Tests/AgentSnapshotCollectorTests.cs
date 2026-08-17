using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class AgentSnapshotCollectorTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CollectAsync_MapsBoundedJobHistoryAndCurrentActivity()
    {
        var runs = new[]
        {
            new SqlAgentRunRow("Nightly ETL", "DOMAIN\\sqlagent", false, 20260817030000, 120),
            new SqlAgentRunRow("Nightly ETL", "DOMAIN\\sqlagent", true, 20260816030000, 60)
        };
        var schedules = new[]
        {
            new SqlAgentScheduleRow(
                "Nightly ETL",
                new DateTime(2026, 8, 18, 3, 0, 0, DateTimeKind.Unspecified),
                false),
            new SqlAgentScheduleRow("Long Running Job", null, true)
        };
        var modules = new SqlHealthModulesRow(
            0, 0, 0, 0, 0, 0,
            2, 0, CollectedAt.AddHours(-1),
            4, 4, 1,
            10_000, 8_000, 2_000,
            0, 0,
            AgentRuns: runs,
            AgentSchedules: schedules);
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("reader", "password")),
            new FakeQuery(new SqlSnapshotRow("SQL01", "17", "Enterprise", null, 3_600, 2, 2, Modules: modules)),
            new FixedTimeProvider(CollectedAt));

        var snapshot = await collector.CollectAsync(Registration());

        Assert.NotNull(snapshot.Jobs?.RecentRuns);
        Assert.Equal(2, snapshot.Jobs!.RecentRuns!.Count);
        Assert.Equal("Nightly ETL", snapshot.Jobs.RecentRuns[0].JobKey);
        Assert.Equal("DOMAIN\\sqlagent", snapshot.Jobs.RecentRuns[0].Owner);
        Assert.False(snapshot.Jobs.RecentRuns[0].Succeeded);
        Assert.Equal(120, snapshot.Jobs.RecentRuns[0].DurationSeconds);

        Assert.NotNull(snapshot.Jobs.Schedules);
        Assert.Equal(2, snapshot.Jobs.Schedules!.Count);
        Assert.Equal("Nightly ETL", snapshot.Jobs.Schedules[0].JobKey);
        Assert.Equal(DateTimeKind.Unspecified, snapshot.Jobs.Schedules[0].NextScheduledRunLocal!.Value.Kind);
        Assert.Equal(new DateTime(2026, 8, 18, 3, 0, 0), snapshot.Jobs.Schedules[0].NextScheduledRunLocal);
        Assert.False(snapshot.Jobs.Schedules[0].IsRunning);
        Assert.True(snapshot.Jobs.Schedules[1].IsRunning);
        Assert.Null(snapshot.Jobs.Schedules[1].NextScheduledRunLocal);
    }

    [Fact]
    public async Task InvalidAgentHistoryFailsClosed()
    {
        var runs = new[] { new SqlAgentRunRow("Job", "owner", false, 20260817030000, -1) };
        var modules = new SqlHealthModulesRow(
            0, 0, 0, 0, 0, 0,
            1, 0, CollectedAt,
            1, 1, 1,
            10, 8, 2,
            0, 0,
            AgentRuns: runs);
        var collector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("reader", "password")),
            new FakeQuery(new SqlSnapshotRow("SQL01", "17", "Enterprise", null, 100, 1, 1, Modules: modules)),
            new FixedTimeProvider(CollectedAt));

        var exception = await Assert.ThrowsAsync<SnapshotCollectionException>(() => collector.CollectAsync(Registration()));

        Assert.Equal(SnapshotCollectionFailure.Failed, exception.Failure);
    }

    [Fact]
    public async Task InvalidOrOverBoundAgentScheduleActivityFailsClosed()
    {
        var invalidModules = new SqlHealthModulesRow(
            0, 0, 0, 0, 0, 0,
            1, 0, CollectedAt,
            1, 1, 0,
            10, 8, 2,
            0, 0,
            AgentSchedules: [new SqlAgentScheduleRow("", null, false)]);
        var invalidCollector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("reader", "password")),
            new FakeQuery(new SqlSnapshotRow("SQL01", "17", "Enterprise", null, 100, 1, 1, Modules: invalidModules)),
            new FixedTimeProvider(CollectedAt));

        var invalid = await Assert.ThrowsAsync<SnapshotCollectionException>(() => invalidCollector.CollectAsync(Registration()));
        Assert.Equal(SnapshotCollectionFailure.Failed, invalid.Failure);

        var tooManyModules = invalidModules with
        {
            AgentSchedules = Enumerable.Range(1, 51)
                .Select(index => new SqlAgentScheduleRow($"Job {index}", null, false))
                .ToArray()
        };
        var tooManyCollector = new SqlServerSnapshotCollector(
            new FakeSecretStore(new SqlLoginSecret("reader", "password")),
            new FakeQuery(new SqlSnapshotRow("SQL01", "17", "Enterprise", null, 100, 1, 1, Modules: tooManyModules)),
            new FixedTimeProvider(CollectedAt));

        var tooMany = await Assert.ThrowsAsync<SnapshotCollectionException>(() => tooManyCollector.CollectAsync(Registration()));
        Assert.Equal(SnapshotCollectionFailure.Failed, tooMany.Failure);
    }

    [Fact]
    public void QueryUsesOnlyBoundedJobSummaryHistoryAndCurrentActivityMetadata()
    {
        var sql = SqlSnapshotQuery.CommandText;

        Assert.Contains("msdb.dbo.sysjobhistory", sql, StringComparison.Ordinal);
        Assert.Contains("h.step_id = 0", sql, StringComparison.Ordinal);
        Assert.Contains("AgentRunsJson", sql, StringComparison.Ordinal);
        Assert.Contains("msdb.dbo.sysjobactivity", sql, StringComparison.Ordinal);
        Assert.Contains("next_scheduled_run_date", sql, StringComparison.Ordinal);
        Assert.Contains("MAX(current_activity.session_id)", sql, StringComparison.Ordinal);
        Assert.Contains("AgentSchedulesJson", sql, StringComparison.Ordinal);
        Assert.Contains("TOP (50)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("sysjobsteps", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sysjobschedules", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sysschedules", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LeastPrivilegeScriptGrantsOnlyReadAccessNeededForAgentEvidence()
    {
        var root = FindRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts/sql/monitored_sql_least_privilege.sql"));

        Assert.Contains("GRANT SELECT ON dbo.sysjobhistory TO MonitorObserverMsdbRole;", script, StringComparison.Ordinal);
        Assert.Contains("GRANT SELECT ON dbo.sysjobactivity TO MonitorObserverMsdbRole;", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SQLAgentReaderRole", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQLAgentOperatorRole", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQLAgentUserRole", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GRANT EXECUTE", script, StringComparison.OrdinalIgnoreCase);
    }

    private static ServerRegistration Registration() => new(
        Guid.NewGuid(), "SQL 01", new SqlServerEndpoint("sql01.internal", port: 1433),
        SqlAuthenticationMode.SqlLogin, new ConnectionSecretReference("sql01-login"),
        true, DateTimeOffset.UtcNow);

    private sealed class FakeSecretStore(SqlLoginSecret? secret) : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default) => ValueTask.FromResult(secret);
    }

    private sealed class FakeQuery(SqlSnapshotRow row) : ISqlSnapshotQuery
    {
        public Task<SqlSnapshotRow> ExecuteAsync(ServerRegistration registration, SqlLoginSecret? secret, CancellationToken cancellationToken) => Task.FromResult(row);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
