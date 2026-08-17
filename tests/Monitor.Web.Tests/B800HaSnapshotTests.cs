using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800HaSnapshotTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Mapper_MapsReplicaAndDatabaseEvidenceWithoutReadinessClaims()
    {
        var mapped = HaEvidenceMapper.Map(new SqlHaRow(
            true,
            2,
            2,
            [
                new SqlHaReplicaRow("FinanceAg", "sql-a", true, "SYNCHRONOUS_COMMIT", "AUTOMATIC", "PRIMARY", "CONNECTED", "ONLINE", "HEALTHY"),
                new SqlHaReplicaRow("FinanceAg", "sql-b", false, "SYNCHRONOUS_COMMIT", "AUTOMATIC", "SECONDARY", "CONNECTED", null, "HEALTHY")
            ],
            [
                new SqlHaDatabaseReplicaRow("FinanceAg", "sql-a", "Finance", true, true, "SYNCHRONIZED", "HEALTHY", false, null, 0, 0, 0),
                new SqlHaDatabaseReplicaRow("FinanceAg", "sql-b", "Finance", false, false, "SYNCHRONIZED", "HEALTHY", false, null, 128, 64, 2)
            ]));

        Assert.True(mapped.IsHadrEnabled);
        Assert.Equal(2, mapped.TotalReplicas);
        Assert.Equal(2, mapped.TotalDatabaseReplicas);
        Assert.False(mapped.ReplicasTruncated);
        Assert.False(mapped.DatabaseReplicasTruncated);
        Assert.Equal(2, mapped.Replicas?.Count);
        Assert.Equal(2, mapped.DatabaseReplicas?.Count);
        Assert.Equal(2, mapped.DatabaseReplicas![1].SecondaryLagSeconds);
    }

    [Fact]
    public void Mapper_AcceptsTruthfulNonHaEmptyEvidence()
    {
        var mapped = HaEvidenceMapper.Map(new SqlHaRow(false, 0, 0, [], []));

        Assert.False(mapped.IsHadrEnabled);
        Assert.Empty(mapped.Replicas!);
        Assert.Empty(mapped.DatabaseReplicas!);
    }

    [Fact]
    public void Mapper_FailsClosedForIncompleteDuplicateOrInvalidEvidence()
    {
        Assert.Throws<InvalidDataException>(() => HaEvidenceMapper.Map(new SqlHaRow(
            true,
            2,
            0,
            [new SqlHaReplicaRow("Ag", "sql-a", true, "SYNCHRONOUS_COMMIT", "AUTOMATIC", "PRIMARY", "CONNECTED", "ONLINE", "HEALTHY")],
            [])));

        Assert.Throws<InvalidDataException>(() => HaEvidenceMapper.Map(new SqlHaRow(
            true,
            2,
            0,
            [
                new SqlHaReplicaRow("Ag", "sql-a", true, "SYNCHRONOUS_COMMIT", "AUTOMATIC", "PRIMARY", "CONNECTED", "ONLINE", "HEALTHY"),
                new SqlHaReplicaRow("Ag", "sql-a", false, "SYNCHRONOUS_COMMIT", "AUTOMATIC", "SECONDARY", "CONNECTED", null, "HEALTHY")
            ],
            [])));

        Assert.Throws<InvalidDataException>(() => HaEvidenceMapper.Map(new SqlHaRow(
            true,
            1,
            1,
            [new SqlHaReplicaRow("Ag", "sql-a", true, "SYNCHRONOUS_COMMIT", "AUTOMATIC", "PRIMARY", "CONNECTED", "ONLINE", "HEALTHY")],
            [new SqlHaDatabaseReplicaRow("Ag", "sql-a", "App", true, true, "SYNCHRONIZED", "HEALTHY", false, null, -1, 0, 0)])));
    }

    [Fact]
    public void Query_IsBoundedReadOnlyAndCollectsNoEndpointClusterOrSqlIdentity()
    {
        var sql = HaSnapshotQuery.CommandText;

        Assert.Contains("TOP (16)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TOP (64)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sys.dm_hadr_availability_replica_states", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sys.dm_hadr_database_replica_states", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secondary_lag_seconds", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("log_send_queue_size", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("redo_queue_size", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint_url", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dm_hadr_cluster_members", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dm_hadr_cluster", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dm_exec_sql_text", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("query_plan", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cache_CollectsHaButPeekRemainsCacheOnly()
    {
        var cache = File.ReadAllText(Path.Combine(Root, "src/Monitor.Web/Services/ServerHealthSnapshotCache.cs"));
        var peekStart = cache.IndexOf("public SnapshotCacheResult? Peek", StringComparison.Ordinal);
        var evictStart = cache.IndexOf("public void Evict", StringComparison.Ordinal);
        var peekBody = cache[peekStart..evictStart];

        Assert.DoesNotContain("HaSnapshotQuery", peekBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", peekBody, StringComparison.Ordinal);
        Assert.Contains("snapshot = await TryEnrichHaAsync(registration, snapshot);", cache, StringComparison.Ordinal);
        Assert.Contains("return snapshot with { Ha = HaEvidenceMapper.Map(row) };", cache, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
