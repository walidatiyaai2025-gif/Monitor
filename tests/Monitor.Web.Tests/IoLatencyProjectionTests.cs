using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IoLatencyProjectionTests
{
    [Fact]
    public void Build_UsesCumulativeCountersAndUptimeWithoutInventingIntervalHistory()
    {
        const long mb = 1024L * 1024L;
        var storage = new StorageHealthSnapshot(
            1_000,
            800,
            200,
            [
                new IoFileSnapshot("db1/data", 100, 100, 1_000, 3_000, 100 * mb, 50 * mb),
                new IoFileSnapshot("db2/log", 10, 0, 500, 0, 0, 0)
            ]);

        var result = IoLatencyProjection.Build(storage, 10, 8);

        Assert.Equal(2, result.Count);
        Assert.Equal("db2/log", result[0].FileKey);
        Assert.Equal(50d, result[0].WeightedLatencyMs);
        Assert.Equal(0d, result[0].ThroughputMbPerSecond);
        Assert.Equal(IoLatencyBand.Severe, result[0].LatencyBand);
        Assert.Equal(B400Severity.Critical, result[0].Severity);
        Assert.True(result[0].Hotspot);

        Assert.Equal("db1/data", result[1].FileKey);
        Assert.Equal(20d, result[1].WeightedLatencyMs);
        Assert.Equal(15d, result[1].ThroughputMbPerSecond);
        Assert.Equal(50d, result[1].WriteSharePercent);
        Assert.Equal(IoLatencyBand.High, result[1].LatencyBand);
        Assert.Equal(B400Severity.Info, result[1].Severity);
        Assert.False(result[1].Hotspot);
    }

    [Fact]
    public void Build_MissingFilesOrUptimeReturnsNoSyntheticHealthyRows()
    {
        Assert.Empty(IoLatencyProjection.Build(new StorageHealthSnapshot(10, 8, 2), 3_600));

        var storage = new StorageHealthSnapshot(
            10,
            8,
            2,
            [new IoFileSnapshot("db/data", 1, 1, 1, 1, 1, 1)]);

        Assert.Empty(IoLatencyProjection.Build(storage, null));
        Assert.Empty(IoLatencyProjection.Build(storage, 0));
    }

    [Fact]
    public void StorageView_WiresCachedB400FileIoWithTruthfulSafetyBoundary()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Views/Operations/Storage.cshtml"));
        var controller = File.ReadAllText(Path.Combine(root, "src/Monitor.Web/Controllers/OperationsController.cs"));

        Assert.Contains("IoLatencyProjection.Build", view, StringComparison.Ordinal);
        Assert.Contains("B400 FILE I/O", view, StringComparison.Ordinal);
        Assert.Contains("Cumulative file latency and throughput", view, StringComparison.Ordinal);
        Assert.Contains("No healthy I/O state is inferred", view, StringComparison.Ordinal);
        Assert.Contains("Physical filesystem paths are never collected or rendered", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("it is not a recent interval rate or persisted trend", view, StringComparison.Ordinal);
        Assert.Contains("GET navigation does not contact monitored SQL", view, StringComparison.Ordinal);
        Assert.Contains("GetHealthModulesAsync(cancellationToken)", controller, StringComparison.Ordinal);
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
