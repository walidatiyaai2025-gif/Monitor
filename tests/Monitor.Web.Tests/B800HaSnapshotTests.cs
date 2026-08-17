using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800HaSnapshotTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Mapper_PreservesReplicaAndQuorumEvidenceWithoutReadinessClaim()
    {
        var mapped = HaEvidenceMapper.Map(new SqlHaRow(
            IsHadrEnabled: true,
            TotalLocalDatabaseReplicas: 1,
            Replicas:
            [
                new SqlHaReplicaRow(
                    "AG01",
                    "AppDb",
                    "SECONDARY",
                    "SYNCHRONIZING",
                    "PARTIALLY_HEALTHY",
                    true,
                    false,
                    false,
                    4L * 1024 * 1024,
                    2L * 1024 * 1024,
                    12)
            ],
            QuorumState: "NORMAL_QUORUM",
            HealthyVotes: 3,
            TotalVotes: 3));

        Assert.True(mapped.IsHadrEnabled);
        Assert.Equal(1, mapped.TotalLocalDatabaseReplicas);
        Assert.False(mapped.IsTruncated);
        Assert.Equal("NORMAL_QUORUM", mapped.QuorumState);
        Assert.Equal(3, mapped.HealthyVotes);
        Assert.Equal(3, mapped.TotalVotes);
        var replica = Assert.Single(mapped.Replicas!);
        Assert.Equal("AG01", replica.GroupKey);
        Assert.Equal("AppDb", replica.DatabaseKey);
        Assert.Equal("SYNCHRONIZING", replica.SynchronizationState);
        Assert.Equal(12, replica.LagSeconds);
    }

    [Fact]
    public void Mapper_AllowsExplicitNonHadrStateWithoutSyntheticHealth()
    {
        var mapped = HaEvidenceMapper.Map(new SqlHaRow(false, 0, [], null, null, null));

        Assert.False(mapped.IsHadrEnabled);
        Assert.Empty(mapped.Replicas!);
        Assert.Null(mapped.QuorumState);
        Assert.Null(mapped.HealthyVotes);
        Assert.Null(mapped.TotalVotes);
        Assert.False(mapped.IsTruncated);
    }

    [Fact]
    public void Mapper_FailsClosedForIncompleteOrInvalidEvidence()
    {
        Assert.Throws<InvalidDataException>(() => HaEvidenceMapper.Map(new SqlHaRow(
            false,
            1,
            [new SqlHaReplicaRow("AG", "Db", null, null, null, null, false, false, null, null, null)],
            null,
            null,
            null)));

        Assert.Throws<InvalidDataException>(() => HaEvidenceMapper.Map(new SqlHaRow(
            true,
            0,
            [],
            "NORMAL_QUORUM",
            2,
            null)));

        Assert.Throws<InvalidDataException>(() => HaEvidenceMapper.Map(new SqlHaRow(
            true,
            1,
            [new SqlHaReplicaRow("AG", "Db", null, null, null, null, false, false, -1, null, null)],
            null,
            null,
            null)));
    }

    [Fact]
    public void Query_IsBoundedReadOnlyAndExcludesReplicaEndpointsAndRemoteNames()
    {
        var sql = HaSnapshotQuery.CommandText;

        Assert.Contains("TOP (50)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SERVERPROPERTY(N'IsHadrEnabled')", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sys.dm_hadr_database_replica_states", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sys.dm_hadr_availability_replica_states", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sys.dm_hadr_cluster_members", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint_url", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("replica_server_name", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("member_name", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dm_exec_sql_text", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("query_plan", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cache_CollectsHaOnlyOnRefreshPathAndNeverFromPeek()
    {
        var cache = File.ReadAllText(Path.Combine(Root, "src/Monitor.Web/Services/ServerHealthSnapshotCache.cs"));
        var peekStart = cache.IndexOf("public SnapshotCacheResult? Peek", StringComparison.Ordinal);
        var evictStart = cache.IndexOf("public void Evict", StringComparison.Ordinal);
        var peekBody = cache[peekStart..evictStart];

        Assert.DoesNotContain("HaSnapshotQuery", peekBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", peekBody, StringComparison.Ordinal);
        Assert.Contains("snapshot = await TryEnrichHaAsync(registration, snapshot);", cache, StringComparison.Ordinal);
        Assert.Contains("return snapshot with { HighAvailability = HaEvidenceMapper.Map(row) };", cache, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
