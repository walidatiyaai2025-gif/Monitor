using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400FleetCorrelationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");

    [Fact] public void B400_091_FleetServerKeyNormalizationIsBounded() => Assert.Equal("SERVER-A", Batch400FleetCorrelation.NormalizeServerKey(" server-a "));
    [Fact] public void B400_092_FleetEnvironmentNormalizationIsStable() => Assert.Equal("PROD", Batch400FleetCorrelation.NormalizeEnvironment("prod"));
    [Fact] public void B400_093_FleetCorrelationWindowIsBounded() => Assert.Equal(TimeSpan.FromHours(24), Batch400FleetCorrelation.ClampWindow(TimeSpan.FromDays(3)));
    [Fact] public void B400_094_FleetBucketRoundsToWindow() { var at = DateTimeOffset.Parse("2026-08-11T06:07:00Z"); Assert.Equal(DateTimeOffset.Parse("2026-08-11T06:05:00Z"), Batch400FleetCorrelation.Bucket(at, TimeSpan.FromMinutes(5))); }
    [Fact] public void B400_095_FleetCorrelationKeyIsStable() { var signal = S("a", "prod", "R1", B400Severity.Warning, 0); Assert.Equal(Batch400FleetCorrelation.CorrelationKey(signal, TimeSpan.FromMinutes(5)), Batch400FleetCorrelation.CorrelationKey(signal, TimeSpan.FromMinutes(5))); }
    [Fact] public void B400_096_FleetSeverityWeightsCriticalHighest() => Assert.True(Batch400FleetCorrelation.SeverityWeight(B400Severity.Critical) > Batch400FleetCorrelation.SeverityWeight(B400Severity.Warning));
    [Fact] public void B400_097_FleetBlastRadiusCountsDistinctServers() => Assert.Equal(2, Batch400FleetCorrelation.BlastRadius([S("a", "prod", "R1", B400Severity.Warning, 0), S("a", "prod", "R1", B400Severity.Warning, 1), S("b", "prod", "R1", B400Severity.Warning, 1)]));
    [Fact] public void B400_098_FleetDominantRuleUsesCountThenName() => Assert.Equal("R1", Batch400FleetCorrelation.DominantRule([S("a", "prod", "R1", B400Severity.Warning, 0), S("b", "prod", "R1", B400Severity.Warning, 0), S("c", "prod", "R2", B400Severity.Warning, 0)]));
    [Fact] public void B400_099_FleetEnvironmentsAreDistinctAndSorted() => Assert.Equal(new[] { "DEV", "PROD" }, Batch400FleetCorrelation.Environments([S("a", "prod", "R1", B400Severity.Info, 0), S("b", "dev", "R1", B400Severity.Info, 0)]));
    [Fact] public void B400_100_FleetCorrelationPreservesCriticalSeverity() { var rows = Batch400FleetCorrelation.Correlate([S("a", "prod", "R1", B400Severity.Critical, 0), S("b", "prod", "R1", B400Severity.Warning, 1), S("c", "prod", "R2", B400Severity.Info, 0)], TimeSpan.FromMinutes(5), 1); Assert.Single(rows); Assert.Equal(B400Severity.Critical, rows[0].Severity); }

    private static FleetSignal S(string server, string environment, string rule, B400Severity severity, int minute) => new(server, environment, rule, severity, Now.AddMinutes(minute));
}
