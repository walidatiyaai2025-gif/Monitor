using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800TempDbSnapshotTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Mapper_MapsCompletePointInTimeEvidenceWithoutTruncation()
    {
        var row = new SqlTempDbRow(
            LogicalCpuCount: 8,
            TotalDataFiles: 2,
            DataFiles:
            [
                new SqlTempDbFileRow(1, "tempdev", 64L * 1024 * 1024, 20L * 1024 * 1024, 100, 50, 120, 80),
                new SqlTempDbFileRow(3, "temp2", 64L * 1024 * 1024, 16L * 1024 * 1024, 90, 40, 110, 70)
            ]);

        var mapped = TempDbEvidenceMapper.Map(row);

        Assert.Equal(8, mapped.LogicalCpuCount);
        Assert.Equal(2, mapped.TotalDataFiles);
        Assert.False(mapped.IsTruncated);
        Assert.Equal(2, mapped.DataFiles?.Count);
        Assert.Equal("tempdev", mapped.DataFiles![0].FileKey);
        Assert.Equal(20L * 1024 * 1024, mapped.DataFiles[0].UsedBytes);
    }

    [Fact]
    public void Mapper_MarksOnlyBoundedOverflowAsTruncated()
    {
        var files = Enumerable.Range(1, TempDbSnapshotQuery.MaxFiles)
            .Select(index => new SqlTempDbFileRow(index, $"temp{index}", 8192, 4096, 0, 0, 0, 0))
            .ToArray();

        var mapped = TempDbEvidenceMapper.Map(new SqlTempDbRow(
            LogicalCpuCount: 64,
            TotalDataFiles: TempDbSnapshotQuery.MaxFiles + 1,
            DataFiles: files));

        Assert.True(mapped.IsTruncated);
        Assert.Equal(TempDbSnapshotQuery.MaxFiles, mapped.DataFiles?.Count);
    }

    [Fact]
    public void Mapper_FailsClosedForInvalidIncompleteOrOverBoundedEvidence()
    {
        Assert.Throws<InvalidDataException>(() => TempDbEvidenceMapper.Map(new SqlTempDbRow(
            4,
            1,
            [new SqlTempDbFileRow(1, "tempdev", 1024, 2048, 0, 0, 0, 0)])));

        Assert.Throws<InvalidDataException>(() => TempDbEvidenceMapper.Map(new SqlTempDbRow(
            4,
            3,
            [
                new SqlTempDbFileRow(1, "tempdev", 8192, 4096, 0, 0, 0, 0),
                new SqlTempDbFileRow(2, "temp2", 8192, 4096, 0, 0, 0, 0)
            ])));

        var tooMany = Enumerable.Range(1, TempDbSnapshotQuery.MaxFiles + 1)
            .Select(index => new SqlTempDbFileRow(index, $"temp{index}", 8192, 0, 0, 0, 0, 0))
            .ToArray();
        Assert.Throws<InvalidDataException>(() => TempDbEvidenceMapper.Map(new SqlTempDbRow(4, tooMany.Length, tooMany)));
    }

    [Fact]
    public void Query_IsBoundedReadOnlyAndNeverCollectsPhysicalPathsOrSqlText()
    {
        var sql = TempDbSnapshotQuery.CommandText;

        Assert.Contains("TOP (32)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tempdb.sys.database_files", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tempdb.sys.dm_db_file_space_usage", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sys.dm_io_virtual_file_stats", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fs.total_page_count", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fs.unallocated_extent_page_count", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("physical_name", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dm_exec_sql_text", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("query_plan", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("growth", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cache_EnrichesOnlyDuringCollectionAndKeepsPeekCacheOnly()
    {
        var cache = File.ReadAllText(Path.Combine(Root, "src/Monitor.Web/Services/ServerHealthSnapshotCache.cs"));
        var peekStart = cache.IndexOf("public SnapshotCacheResult? Peek", StringComparison.Ordinal);
        var evictStart = cache.IndexOf("public void Evict", StringComparison.Ordinal);
        var peekBody = cache[peekStart..evictStart];

        Assert.DoesNotContain("TempDbSnapshotQuery", peekBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", peekBody, StringComparison.Ordinal);
        Assert.Contains("snapshot = await TryEnrichTempDbAsync(registration, snapshot);", cache, StringComparison.Ordinal);
        Assert.Contains("IServiceProvider? services = null", cache, StringComparison.Ordinal);
        Assert.Contains("GetService(typeof(IConnectionSecretStore))", cache, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
