using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400IoLatencyTests
{
    [Fact] public void B400_051_IoFileKeyNormalizesSlashes() => Assert.Equal("C:/DATA/DB.MDF", Batch400IoLatency.NormalizeFileKey("C:\\DATA\\DB.MDF"));
    [Fact] public void B400_052_IoLatencyRejectsNonFiniteValues() => Assert.Equal(0, Batch400IoLatency.ClampLatency(double.PositiveInfinity));
    [Fact] public void B400_053_IoThroughputAddsReadAndWrite() => Assert.Equal(30, Batch400IoLatency.Throughput(new("f", 1, 1, 10, 20, 1, 1)));
    [Fact] public void B400_054_IoWeightedLatencyUsesOperationCounts() => Assert.Equal(15, Batch400IoLatency.WeightedLatency(new("f", 10, 20, 0, 0, 1, 1)));
    [Fact] public void B400_055_IoWriteShareUsesOperationCounts() => Assert.Equal(75, Batch400IoLatency.WriteSharePercent(new("f", 1, 1, 0, 0, 1, 3)));
    [Fact] public void B400_056_IoLatencyBandDetectsSevereStorage() => Assert.Equal(IoLatencyBand.Severe, Batch400IoLatency.LatencyBand(80));
    [Fact] public void B400_057_IoScoreIsBounded() => Assert.Equal(100, Batch400IoLatency.Score(new("f", 100, 100, 1, 1, 10, 10)));
    [Fact] public void B400_058_IoSeverityUsesScore() => Assert.Equal(B400Severity.Warning, Batch400IoLatency.Severity(60));
    [Fact] public void B400_059_IoFingerprintIsOpaqueAndStable() { var value = Batch400IoLatency.Fingerprint("f"); Assert.Equal(16, value.Length); Assert.Equal(value, Batch400IoLatency.Fingerprint("f")); }
    [Fact] public void B400_060_IoTopFilesAreSortedAndBounded() { var rows = Batch400IoLatency.TopFiles([new("slow", 100, 100, 1, 1, 10, 10), new("fast", 1, 1, 1, 1, 10, 10)], 1); Assert.Single(rows); Assert.Equal("slow", rows[0].FileKey); }
}
