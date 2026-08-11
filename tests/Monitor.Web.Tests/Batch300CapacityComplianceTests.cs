using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300CapacityComplianceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");
    private static readonly Guid ServerId = Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb");
    private static CapacityPolicy Policy => new(1_000_000, 80, 90, 24, 100, 15);

    [Fact]
    public void B300_051_StorageUtilizationUsesConfiguredCapacityCeiling()
    {
        Assert.Equal(50, CapacityCompliance.StorageUtilizationPercent(Snapshot(storage: new(500_000, 400_000, 100_000)), Policy));
        Assert.Equal(100, CapacityCompliance.StorageUtilizationPercent(Snapshot(storage: new(2_000_000, 1_500_000, 500_000)), Policy));
    }

    [Fact]
    public void B300_052_CapacityRiskClassifiesWarningAndCriticalThresholds()
    {
        Assert.Equal(ComplianceState.Compliant, CapacityCompliance.StorageState(Snapshot(storage: new(790_000, 0, 0)), Policy));
        Assert.Equal(ComplianceState.Warning, CapacityCompliance.StorageState(Snapshot(storage: new(800_000, 0, 0)), Policy));
        Assert.Equal(ComplianceState.Critical, CapacityCompliance.StorageState(Snapshot(storage: new(900_000, 0, 0)), Policy));
    }

    [Fact]
    public void B300_053_BackupComplianceTreatsMissingBackupsAsCritical()
    {
        Assert.Equal(ComplianceState.Critical, CapacityCompliance.BackupState(Snapshot(backups: new(5, 1, Now)), Policy, Now));
        Assert.Equal(ComplianceState.Compliant, CapacityCompliance.BackupState(Snapshot(backups: new(6, 0, Now.AddHours(-2))), Policy, Now));
    }

    [Fact]
    public void B300_054_BackupAgeUsesWarningThenCriticalBounds()
    {
        Assert.Equal(ComplianceState.Warning, CapacityCompliance.BackupState(Snapshot(backups: new(6, 0, Now.AddHours(-30))), Policy, Now));
        Assert.Equal(ComplianceState.Critical, CapacityCompliance.BackupState(Snapshot(backups: new(6, 0, Now.AddHours(-60))), Policy, Now));
    }

    [Fact]
    public void B300_055_DatabaseOnlineRatioClassifiesPartialAvailability()
    {
        Assert.Equal(ComplianceState.Compliant, CapacityCompliance.DatabaseState(Snapshot(total: 10, online: 10), Policy));
        Assert.Equal(ComplianceState.Warning, CapacityCompliance.DatabaseState(Snapshot(total: 10, online: 9), Policy));
        Assert.Equal(ComplianceState.Critical, CapacityCompliance.DatabaseState(Snapshot(total: 10, online: 7), Policy));
    }

    [Fact]
    public void B300_056_MemoryHeadroomUsesAvailablePhysicalMemoryAndPressureFlags()
    {
        Assert.Equal(ComplianceState.Compliant, CapacityCompliance.MemoryState(Snapshot(memory: Memory(20)), Policy));
        Assert.Equal(ComplianceState.Warning, CapacityCompliance.MemoryState(Snapshot(memory: Memory(10)), Policy));
        Assert.Equal(ComplianceState.Critical, CapacityCompliance.MemoryState(Snapshot(memory: Memory(5)), Policy));
        Assert.Equal(ComplianceState.Critical, CapacityCompliance.MemoryState(Snapshot(memory: Memory(30, low: true)), Policy));
    }

    [Fact]
    public void B300_057_FleetCapacityEvaluationCombinesFourIndependentControls()
    {
        var projection = CapacityCompliance.Evaluate(Snapshot(
            storage: new(950_000, 0, 0),
            backups: new(10, 0, Now),
            memory: Memory(20)), Policy, Now);
        Assert.Equal(95, projection.StorageUtilizationPercent);
        Assert.Equal(ComplianceState.Critical, projection.StorageState);
        Assert.Equal(ComplianceState.Compliant, projection.BackupState);
        Assert.Equal(75, projection.Score);
    }

    [Fact]
    public void B300_058_EnvironmentRollupSeparatesCompliantWarningAndCriticalServers()
    {
        var good = CapacityCompliance.Evaluate(Snapshot(), Policy, Now);
        var warning = CapacityCompliance.Evaluate(Snapshot(storage: new(850_000, 0, 0)), Policy, Now);
        var critical = CapacityCompliance.Evaluate(Snapshot(storage: new(950_000, 0, 0)), Policy, Now);
        var rollup = Assert.Single(CapacityCompliance.Rollup([
            (ServerEnvironmentClass.Production, good),
            (ServerEnvironmentClass.Production, warning),
            (ServerEnvironmentClass.Production, critical)]));
        Assert.Equal(3, rollup.Servers);
        Assert.Equal(1, rollup.Compliant);
        Assert.Equal(1, rollup.Warning);
        Assert.Equal(1, rollup.Critical);
    }

    [Fact]
    public void B300_059_ComplianceScoreIsDeterministicAndBounded()
    {
        var snapshot = Snapshot(storage: new(850_000, 0, 0), backups: new(10, 0, Now), memory: Memory(10));
        var first = CapacityCompliance.Evaluate(snapshot, Policy, Now);
        var second = CapacityCompliance.Evaluate(snapshot, Policy, Now);
        Assert.Equal(first.Score, second.Score);
        Assert.InRange(first.Score, 0, 100);
    }

    [Fact]
    public void B300_060_PolicyRejectsUnsafeThresholdConfiguration()
    {
        Policy.Validate();
        Assert.Throws<ArgumentOutOfRangeException>(() => new CapacityPolicy(0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CapacityPolicy(1000, 90, 90).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CapacityPolicy(1000, MaxBackupAgeHours: 169).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CapacityPolicy(1000, MinimumMemoryHeadroomPercent: 91).Validate());
    }

    private static ServerHealthSnapshot Snapshot(
        int total = 10,
        int online = 10,
        StorageHealthSnapshot? storage = null,
        BackupHealthSnapshot? backups = null,
        MemoryHealthSnapshot? memory = null) =>
        new(ServerId, "SQL", "17.0", "Enterprise", null, 1000, total, online, Now,
            memory ?? Memory(25), null, backups ?? new(10, 0, Now), null,
            storage ?? new(500_000, 400_000, 100_000), null, null);

    private static MemoryHealthSnapshot Memory(int headroomPercent, bool low = false) =>
        new(1_000_000, headroomPercent * 10_000L, 700_000, 70, low, false, "bounded state");
}
