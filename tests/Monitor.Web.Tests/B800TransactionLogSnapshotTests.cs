using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800TransactionLogSnapshotTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Mapper_MapsDetailedAndPartialRowsWithoutInventingMissingStats()
    {
        var mapped = TransactionLogEvidenceMapper.Map(new SqlTransactionLogRow(
            2,
            [
                new SqlTransactionLogDatabaseRow("AppDb", "FULL", 128L * 1024 * 1024, 32L * 1024 * 1024, 24, 6, "NOTHING", 120, true),
                new SqlTransactionLogDatabaseRow("ReportingReplica", "FULL", null, null, null, null, null, 300, false)
            ]));

        Assert.Equal(2, mapped.TotalDatabases);
        Assert.False(mapped.IsTruncated);
        Assert.Equal(2, mapped.Databases?.Count);
        Assert.True(mapped.Databases![0].HasDetailedStats);
        Assert.False(mapped.Databases[1].HasDetailedStats);
        Assert.Null(mapped.Databases[1].TotalLogSizeBytes);
        Assert.Equal(300, mapped.Databases[1].LogBackupAgeSeconds);
    }

    [Fact]
    public void Mapper_RequiresCompleteBoundedRowSetAndConsistentDetailedStats()
    {
        Assert.Throws<InvalidDataException>(() => TransactionLogEvidenceMapper.Map(new SqlTransactionLogRow(
            2,
            [new SqlTransactionLogDatabaseRow("OnlyOne", "FULL", 8192, 4096, 8, 4, "NOTHING", 30, true)])));

        Assert.Throws<InvalidDataException>(() => TransactionLogEvidenceMapper.Map(new SqlTransactionLogRow(
            1,
            [new SqlTransactionLogDatabaseRow("BadSize", "FULL", 4096, 8192, 8, 4, "NOTHING", 30, true)])));

        Assert.Throws<InvalidDataException>(() => TransactionLogEvidenceMapper.Map(new SqlTransactionLogRow(
            1,
            [new SqlTransactionLogDatabaseRow("BadFlag", "FULL", 8192, 4096, 8, 4, "NOTHING", 30, false)])));
    }

    [Fact]
    public void Mapper_MarksOnlyBoundedOverflowAsTruncated()
    {
        var rows = Enumerable.Range(1, TransactionLogSnapshotQuery.MaxDatabases)
            .Select(index => new SqlTransactionLogDatabaseRow($"Db{index:00}", "SIMPLE", 8192, 4096, 8, 4, "CHECKPOINT", null, true))
            .ToArray();

        var mapped = TransactionLogEvidenceMapper.Map(new SqlTransactionLogRow(
            TransactionLogSnapshotQuery.MaxDatabases + 1,
            rows));

        Assert.True(mapped.IsTruncated);
        Assert.Equal(TransactionLogSnapshotQuery.MaxDatabases, mapped.Databases?.Count);
    }

    [Fact]
    public void Query_IsBoundedReadOnlyAndCollectsNoIdentityOrSqlText()
    {
        var sql = TransactionLogSnapshotQuery.CommandText;

        Assert.Contains("TOP (50)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sys.dm_db_log_stats", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("total_vlf_count", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("active_vlf_count", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("active_log_size_mb", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("log_truncation_holdup_reason", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DATEDIFF_BIG(SECOND, ls.log_backup_time, SYSDATETIME())", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DECLARE @TransactionLogEvidence TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INSERT INTO @TransactionLogEvidence", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(SELECT COUNT(*) FROM @TransactionLogEvidence)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("(SELECT COUNT(*) FROM sys.databases", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, sql.Split("FROM sys.databases AS d", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("physical_name", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("backupset", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dm_tran_database_transactions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dm_exec_sql_text", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("query_plan", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("growth", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cache_CollectsTransactionLogsButPeekRemainsCacheOnly()
    {
        var cache = File.ReadAllText(Path.Combine(Root, "src/Monitor.Web/Services/ServerHealthSnapshotCache.cs"));
        var peekStart = cache.IndexOf("public SnapshotCacheResult? Peek", StringComparison.Ordinal);
        var evictStart = cache.IndexOf("public void Evict", StringComparison.Ordinal);
        var peekBody = cache[peekStart..evictStart];

        Assert.DoesNotContain("TransactionLogSnapshotQuery", peekBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", peekBody, StringComparison.Ordinal);
        Assert.Contains("snapshot = await TryEnrichTransactionLogsAsync(registration, snapshot);", cache, StringComparison.Ordinal);
        Assert.Contains("return snapshot with { TransactionLogs = TransactionLogEvidenceMapper.Map(row) };", cache, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
