using Xunit;

namespace Monitor.Web.Tests;

public sealed class GovernedSqlSnapshotQueryParityTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void GovernedRuntimeQuery_MapsAllExtendedSnapshotEvidence()
    {
        var governed = File.ReadAllText(Path.Combine(Root, "src/Monitor.Web/Services/GovernedSqlSnapshotQuery.cs"));

        Assert.Contains("reader.IsDBNull(34) ? null : reader.GetInt64(34)", governed, StringComparison.Ordinal);
        Assert.Contains("reader.IsDBNull(40) ? null : reader.GetInt64(40)", governed, StringComparison.Ordinal);
        Assert.Contains("ReadWaitStats(reader, 41)", governed, StringComparison.Ordinal);
        Assert.Contains("ReadIoFiles(reader, 42)", governed, StringComparison.Ordinal);
        Assert.Contains("ReadAgentRuns(reader, 43)", governed, StringComparison.Ordinal);
        Assert.Contains("ReadDatabaseStates(reader, 44)", governed, StringComparison.Ordinal);
        Assert.Contains("ReadAgentSchedules(reader, 45)", governed, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Deserialize<T[]>", governed, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionDiPath_UsesGovernedQueryAndSharedCommandText()
    {
        var program = File.ReadAllText(Path.Combine(Root, "src/Monitor.Web/Program.cs"));
        var governed = File.ReadAllText(Path.Combine(Root, "src/Monitor.Web/Services/GovernedSqlSnapshotQuery.cs"));
        var collector = File.ReadAllText(Path.Combine(Root, "src/Monitor.Web/Services/SqlServerSnapshotCollector.cs"));

        Assert.Contains("AddSingleton<ISqlSnapshotQuery, GovernedSqlSnapshotQuery>()", program, StringComparison.Ordinal);
        Assert.Contains("command.CommandText = SqlSnapshotQuery.CommandText", governed, StringComparison.Ordinal);
        Assert.Contains("AS WaitStatsJson", collector, StringComparison.Ordinal);
        Assert.Contains("AS IoFilesJson", collector, StringComparison.Ordinal);
        Assert.Contains("AS AgentRunsJson", collector, StringComparison.Ordinal);
        Assert.Contains("AS DatabaseStatesJson", collector, StringComparison.Ordinal);
        Assert.Contains("AS AgentSchedulesJson", collector, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
