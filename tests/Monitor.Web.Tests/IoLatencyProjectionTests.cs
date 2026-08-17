using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IoLatencyProjectionTests
{
    [Fact]
    public void Build_NormalizesCumulativeIoBySqlUptime()
    {
        var storage = new StorageHealthSnapshot(
            10_000,
            8_000,
            2_000,
            [
                new IoFileSnapshot("AppDb/AppDb_Data", 100, 50, 2_000, 2_500, 100 * 1024 * 1024L, 50 * 1024 * 1024L)
            ]);

        var result = IoLatencyProjection.Build(storage, 100, 8);

        var file = Assert.Single(result);
        Assert.Equal("AppDb/AppDb_Data", file.FileKey);
        Assert.Equal(30d, file.WeightedLatencyMs);
        Assert.Equal(1.5d, file.ThroughputMbPerSecond);
        Assert.Equal(33.33d, file.WriteSharePercent);
        Assert.Equal(IoLatencyBand.High, file.LatencyBand);
        Assert.Equal(B400Severity.Warning, file.Severity);
        Assert.True(file.Hotspot);
    }

    [Fact]
    public void Build_MissingIoOrUptimeDoesNotInventHealthyState()
    {
        Assert.Empty(IoLatencyProjection.Build(new StorageHealthSnapshot(1, 1, 0), 100));
        Assert.Empty(IoLatencyProjection.Build(new StorageHealthSnapshot(1, 1, 0, [new IoFileSnapshot("db/file", 1, 0, 1, 0, 1, 0)]), null));
        Assert.Empty(IoLatencyProjection.Build(new StorageHealthSnapshot(1, 1, 0, [new IoFileSnapshot("db/file", 1, 0, 1, 0, 1, 0)]), 0));
    }

    [Fact]
    public void StorageView_WiresB400IoWithoutPhysicalPathsOrBrowserSql()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Views/Operations/Storage.cshtml"));
        var collector = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Services/SqlServerSnapshotCollector.cs"));

        Assert.Contains("IoLatencyProjection.Build", view, StringComparison.Ordinal);
        Assert.Contains("B400 FILE I/O", view, StringComparison.Ordinal);
        Assert.Contains("physical filesystem paths are never collected", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sys.dm_io_virtual_file_stats", collector, StringComparison.Ordinal);
        Assert.DoesNotContain("physical_name", collector, StringComparison.OrdinalIgnoreCase);
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
