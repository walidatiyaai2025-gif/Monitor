using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400TempDbPressureTests
{
    [Fact] public void B400_031_TempDbSampleNormalizationClampsUsed() { var sample = Batch400TempDbPressure.Normalize(new(1, 100, 150, 0, 1, 1)); Assert.Equal(100, sample.UsedMb); }
    [Fact] public void B400_032_TempDbUsedPercentUsesAllFiles() => Assert.Equal(50, Batch400TempDbPressure.UsedPercent([new(1, 100, 50, 0, 0, 0), new(2, 100, 50, 0, 0, 0)]));
    [Fact] public void B400_033_TempDbSizeImbalanceIsDetected() => Assert.True(Batch400TempDbPressure.SizeImbalancePercent([new(1, 100, 50, 0, 0, 0), new(2, 200, 50, 0, 0, 0)]) > 0);
    [Fact] public void B400_034_TempDbUsedImbalanceIsDetected() => Assert.Equal(50, Batch400TempDbPressure.UsedImbalancePercent([new(1, 100, 100, 0, 0, 0), new(2, 100, 50, 0, 0, 0)]));
    [Fact] public void B400_035_TempDbGrowthAggregatesFiles() => Assert.Equal(15, Batch400TempDbPressure.GrowthMbPerHour([new(1, 100, 50, 10, 0, 0), new(2, 100, 50, 5, 0, 0)]));
    [Fact] public void B400_036_TempDbLatencyAveragesReadWrite() => Assert.Equal(15, Batch400TempDbPressure.AverageLatencyMs([new(1, 100, 50, 0, 10, 20)]));
    [Fact] public void B400_037_TempDbAllocationContentionIsRateBased() => Assert.Equal(10, Batch400TempDbPressure.AllocationContentionScore(400, 300, 300, TimeSpan.FromSeconds(10)));
    [Fact] public void B400_038_TempDbRecommendedFilesAreBounded() => Assert.Equal(8, Batch400TempDbPressure.RecommendedFileCount(16, 1));
    [Fact] public void B400_039_TempDbSeverityUsesScore() => Assert.Equal(B400Severity.Critical, Batch400TempDbPressure.Severity(80));
    [Fact] public void B400_040_TempDbSummaryMarksHotspots() { var result = Batch400TempDbPressure.Summarize([new(1, 100, 99, 100, 50, 50)], 10000, 10000, 10000, TimeSpan.FromSeconds(10), 8); Assert.True(result.Hotspot); }
}
