using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400AcceptanceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");

    [Fact] public void B400_001_NormalizeWaitTypeIsBoundedAndSafe() => Assert.Equal("PAGEIOLATCH_SH", Batch400WaitIntelligence.NormalizeWaitType(" pageiolatch sh "));
    [Fact] public void B400_002_ClassifyWaitTypeDetectsLocks() => Assert.Equal(WaitCategory.Lock, Batch400WaitIntelligence.Classify("LCK_M_X"));
    [Fact] public void B400_003_BenignWaitsAreIgnored() => Assert.True(Batch400WaitIntelligence.IsBenign("SLEEP_TASK"));
    [Fact] public void B400_004_WaitRateUsesIntervalSeconds() => Assert.Equal(100, Batch400WaitIntelligence.RatePerSecond(new("WRITELOG", 1000, 0, 1, TimeSpan.FromSeconds(10))));
    [Fact] public void B400_005_SignalPercentIsBounded() => Assert.Equal(25, Batch400WaitIntelligence.SignalPercent(new("SOS_SCHEDULER_YIELD", 1000, 250, 1, TimeSpan.FromSeconds(10))));
    [Fact] public void B400_006_WaitShareUsesActionableTotal() { var a = new WaitSample("WRITELOG", 750, 0, 1, TimeSpan.FromSeconds(10)); var all = new[] { a, new WaitSample("LCK_M_X", 250, 0, 1, TimeSpan.FromSeconds(10)), new WaitSample("SLEEP_TASK", 10000, 0, 1, TimeSpan.FromSeconds(10)) }; Assert.Equal(75, Batch400WaitIntelligence.SharePercent(a, all)); }
    [Fact] public void B400_007_WaitScoreIsDeterministic() { var a = new WaitSample("WRITELOG", 1000, 200, 1, TimeSpan.FromSeconds(10)); var score = Batch400WaitIntelligence.Score(a, new[] { a }); Assert.InRange(score, 1, 100); }
    [Fact] public void B400_008_WaitSeverityUsesExplicitThresholds() { Assert.Equal(B400Severity.Critical, Batch400WaitIntelligence.Severity(80)); Assert.Equal(B400Severity.Warning, Batch400WaitIntelligence.Severity(50)); }
    [Fact] public void B400_009_WaitFingerprintIsStableAndOpaque() { var value = Batch400WaitIntelligence.Fingerprint("WRITELOG"); Assert.Equal(16, value.Length); Assert.Equal(value, Batch400WaitIntelligence.Fingerprint("writelog")); }
    [Fact] public void B400_010_WaitSummaryExcludesBenignAndSorts() { var list = Batch400WaitIntelligence.Summarize([new("SLEEP_TASK", 99999, 0, 1, TimeSpan.FromSeconds(10)), new("WRITELOG", 1000, 0, 1, TimeSpan.FromSeconds(10)), new("LCK_M_X", 100, 0, 1, TimeSpan.FromSeconds(10))]); Assert.Equal(2, list.Count); Assert.Equal("WRITELOG", list[0].WaitType); }

    [Fact] public void B400_011_QueryKeyNormalizationIsBounded() => Assert.Equal(96, Batch400QueryRegression.NormalizeQueryKey(new string('x', 200)).Length);
    [Fact] public void B400_012_PercentDeltaHandlesBaseline() => Assert.Equal(100, Batch400QueryRegression.PercentDelta(10, 20));
    [Fact] public void B400_013_DurationDeltaUsesMetrics() => Assert.Equal(50, Batch400QueryRegression.DurationDelta(Q(10, 5, 100), Q(15, 5, 100)));
    [Fact] public void B400_014_CpuDeltaUsesMetrics() => Assert.Equal(100, Batch400QueryRegression.CpuDelta(Q(10, 5, 100), Q(10, 10, 100)));
    [Fact] public void B400_015_ReadDeltaUsesMetrics() => Assert.Equal(50, Batch400QueryRegression.ReadDelta(Q(10, 5, 100), Q(10, 5, 150)));
    [Fact] public void B400_016_PlanChangeRequiresTwoKnownHashes() => Assert.True(Batch400QueryRegression.PlanChanged(Q(10, 5, 100, "A"), Q(10, 5, 100, "B")));
    [Fact] public void B400_017_RegressionScoreIsBounded() => Assert.Equal(100, Batch400QueryRegression.Score(Q(10, 10, 10, "A"), Q(100, 100, 100, "B")));
    [Fact] public void B400_018_QuerySeverityUsesRiskBands() => Assert.Equal(B400Severity.Critical, Batch400QueryRegression.Severity(90));
    [Fact] public void B400_019_QueryCandidateRequiresExecutions() => Assert.True(Batch400QueryRegression.IsRegressionCandidate(Q(10, 10, 10), Q(100, 100, 100)));
    [Fact] public void B400_020_TopRegressionsAreBoundedAndOrdered() { var rows = Batch400QueryRegression.TopRegressions([(Q(10, 10, 10), Q(100, 100, 100)), (Q(10, 10, 10), Q(20, 20, 20))], 1); Assert.Single(rows); Assert.True(rows[0].Score > 0); }

    [Fact] public void B400_021_TempDbSampleNormalizationClampsUsed() { var sample = Batch400TempDbPressure.Normalize(new(1, 100, 150, 0, 1, 1)); Assert.Equal(100, sample.UsedMb); }
    [Fact] public void B400_022_TempDbUsedPercentUsesAllFiles() => Assert.Equal(50, Batch400TempDbPressure.UsedPercent([new(1, 100, 50, 0, 0, 0), new(2, 100, 50, 0, 0, 0)]));
    [Fact] public void B400_023_TempDbSizeImbalanceIsDetected() => Assert.True(Batch400TempDbPressure.SizeImbalancePercent([new(1, 100, 50, 0, 0, 0), new(2, 200, 50, 0, 0, 0)]) > 0);
    [Fact] public void B400_024_TempDbUsedImbalanceIsDetected() => Assert.Equal(50, Batch400TempDbPressure.UsedImbalancePercent([new(1, 100, 100, 0, 0, 0), new(2, 100, 50, 0, 0, 0)]));
    [Fact] public void B400_025_TempDbGrowthAggregatesFiles() => Assert.Equal(15, Batch400TempDbPressure.GrowthMbPerHour([new(1, 100, 50, 10, 0, 0), new(2, 100, 50, 5, 0, 0)]));
    [Fact] public void B400_026_TempDbLatencyAveragesReadWrite() => Assert.Equal(15, Batch400TempDbPressure.AverageLatencyMs([new(1, 100, 50, 0, 10, 20)]));
    [Fact] public void B400_027_TempDbAllocationContentionIsRateBased() => Assert.Equal(10, Batch400TempDbPressure.AllocationContentionScore(400, 300, 300, TimeSpan.FromSeconds(10)));
    [Fact] public void B400_028_TempDbRecommendedFilesAreBounded() => Assert.Equal(8, Batch400TempDbPressure.RecommendedFileCount(16, 1));
    [Fact] public void B400_029_TempDbSeverityUsesScore() => Assert.Equal(B400Severity.Critical, Batch400TempDbPressure.Severity(80));
    [Fact] public void B400_030_TempDbSummaryMarksHotspots() { var result = Batch400TempDbPressure.Summarize([new(1, 100, 99, 100, 50, 50)], 10000, 10000, 10000, TimeSpan.FromSeconds(10), 8); Assert.True(result.Hotspot); }

    [Fact] public void B400_031_LogUsedPercentIsBounded() => Assert.Equal(75, Batch400TransactionLogHealth.UsedPercent(75, 100));
    [Fact] public void B400_032_LogVlfBandDetectsExtremeCounts() => Assert.Equal(LogVlfBand.Extreme, Batch400TransactionLogHealth.VlfBand(1200));
    [Fact] public void B400_033_LogReuseWaitIsNormalized() => Assert.Equal("ACTIVE_TRANSACTION", Batch400TransactionLogHealth.NormalizeReuseWait("active transaction"));
    [Fact] public void B400_034_ActiveTransactionBandUsesAge() => Assert.Equal(LogActivityBand.Extreme, Batch400TransactionLogHealth.ActiveTransactionBand(TimeSpan.FromHours(3)));
    [Fact] public void B400_035_LogBackupOverdueHonorsRecoveryRequirement() => Assert.True(Batch400TransactionLogHealth.LogBackupOverdue(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15), true));
    [Fact] public void B400_036_LogGrowthBandDetectsRapidGrowth() => Assert.Equal(LogGrowthBand.Rapid, Batch400TransactionLogHealth.GrowthBand(200));
    [Fact] public void B400_037_LogRiskScoreCombinesSignals() => Assert.True(Batch400TransactionLogHealth.Score(95, 1200, "ACTIVE_TRANSACTION", TimeSpan.FromHours(3), true, 200) > 75);
    [Fact] public void B400_038_LogSeverityUsesExplicitThresholds() => Assert.Equal(B400Severity.Warning, Batch400TransactionLogHealth.Severity(60));
    [Fact] public void B400_039_LogTruncationBlockedExcludesNothingCheckpoint() { Assert.True(Batch400TransactionLogHealth.TruncationBlocked("ACTIVE_TRANSACTION")); Assert.False(Batch400TransactionLogHealth.TruncationBlocked("NOTHING")); }
    [Fact] public void B400_040_LogSummaryReturnsBoundedReason() { var result = Batch400TransactionLogHealth.Summarize(95, 100, 1200, "ACTIVE_TRANSACTION", TimeSpan.FromHours(3), TimeSpan.FromHours(1), TimeSpan.FromMinutes(15), true, 200); Assert.NotEmpty(result.Reason); Assert.True(result.TruncationBlocked); }

    [Fact] public void B400_041_IoFileKeyNormalizesSlashes() => Assert.Equal("C:/DATA/DB.MDF", Batch400IoLatency.NormalizeFileKey("C:\\DATA\\DB.MDF"));
    [Fact] public void B400_042_IoLatencyRejectsNonFiniteValues() => Assert.Equal(0, Batch400IoLatency.ClampLatency(double.PositiveInfinity));
    [Fact] public void B400_043_IoThroughputAddsReadAndWrite() => Assert.Equal(30, Batch400IoLatency.Throughput(new("f", 1, 1, 10, 20, 1, 1)));
    [Fact] public void B400_044_IoWeightedLatencyUsesOperationCounts() => Assert.Equal(15, Batch400IoLatency.WeightedLatency(new("f", 10, 20, 0, 0, 1, 1)));
    [Fact] public void B400_045_IoWriteShareUsesOperationCounts() => Assert.Equal(75, Batch400IoLatency.WriteSharePercent(new("f", 1, 1, 0, 0, 1, 3)));
    [Fact] public void B400_046_IoLatencyBandDetectsSevereStorage() => Assert.Equal(IoLatencyBand.Severe, Batch400IoLatency.LatencyBand(80));
    [Fact] public void B400_047_IoScoreIsBounded() => Assert.Equal(100, Batch400IoLatency.Score(new("f", 100, 100, 1, 1, 10, 10)));
    [Fact] public void B400_048_IoSeverityUsesScore() => Assert.Equal(B400Severity.Warning, Batch400IoLatency.Severity(60));
    [Fact] public void B400_049_IoFingerprintIsOpaqueAndStable() { var value = Batch400IoLatency.Fingerprint("f"); Assert.Equal(16, value.Length); Assert.Equal(value, Batch400IoLatency.Fingerprint("f")); }
    [Fact] public void B400_050_IoTopFilesAreSortedAndBounded() { var rows = Batch400IoLatency.TopFiles([new("slow", 100, 100, 1, 1, 10, 10), new("fast", 1, 1, 1, 1, 10, 10)], 1); Assert.Single(rows); Assert.Equal("slow", rows[0].FileKey); }

    [Fact] public void B400_051_AgentOwnerNormalizationHasFallback() => Assert.Equal("UNASSIGNED", Batch400AgentReliability.NormalizeOwner(" "));
    [Fact] public void B400_052_AgentSuccessRateUsesHistory() => Assert.Equal(50, Batch400AgentReliability.SuccessRate([Run(true, 0, 1), Run(false, 1, 1)]));
    [Fact] public void B400_053_AgentFailureStreakStopsAtSuccess() => Assert.Equal(2, Batch400AgentReliability.FailureStreak([Run(false, 2, 1), Run(false, 1, 1), Run(true, 0, 1)]));
    [Fact] public void B400_054_AgentP95DurationIsDeterministic() { var runs = Enumerable.Range(1, 20).Select(i => Run(true, i, i)); Assert.Equal(TimeSpan.FromSeconds(19), Batch400AgentReliability.P95Duration(runs)); }
    [Fact] public void B400_055_AgentLatenessNeverGoesNegative() => Assert.Equal(TimeSpan.Zero, Batch400AgentReliability.Lateness(Now, Now.AddMinutes(5)));
    [Fact] public void B400_056_AgentDurationRegressionUsesBaseline() => Assert.Equal(100, Batch400AgentReliability.DurationRegression(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)));
    [Fact] public void B400_057_AgentReliabilityScoreCombinesFailures() { var score = Batch400AgentReliability.ReliabilityScore([Run(false, 2, 1), Run(false, 1, 1)], TimeSpan.FromHours(1), 100); Assert.True(score > 40); }
    [Fact] public void B400_058_AgentSeverityUsesRiskBands() => Assert.Equal(B400Severity.Critical, Batch400AgentReliability.Severity(80));
    [Fact] public void B400_059_AgentAlertWorthyDetectsFailureStreak() => Assert.True(Batch400AgentReliability.AlertWorthy(0, 2, TimeSpan.Zero));
    [Fact] public void B400_060_AgentSummaryBoundsHistory() { var runs = Enumerable.Range(0, 120).Select(i => Run(true, i, 1)); var result = Batch400AgentReliability.Summarize("DBA", runs, Now.AddHours(200), Now, TimeSpan.FromSeconds(1)); Assert.Equal(100, result.RunsEvaluated); }

    [Fact] public void B400_061_HaStateNormalizationRecognizesSynchronized() => Assert.Equal(ReplicaSyncState.Synchronized, Batch400HaReadiness.NormalizeState("synchronized"));
    [Fact] public void B400_062_HaLagBandDetectsCriticalLag() => Assert.Equal(HaLagBand.Critical, Batch400HaReadiness.LagBand(300));
    [Fact] public void B400_063_HaQueueScoreUsesWorstQueue() => Assert.Equal(10, Batch400HaReadiness.QueueScore(100, 50));
    [Fact] public void B400_064_HaSyncScoreFailsClosedOnDisconnect() => Assert.Equal(100, Batch400HaReadiness.SyncScore("SYNCHRONIZED", false));
    [Fact] public void B400_065_HaFailoverReadinessRequiresSyncAndQuorum() => Assert.True(Batch400HaReadiness.FailoverReady(new("SYNCHRONIZED", 10, 10, 1, true, true), true));
    [Fact] public void B400_066_HaRpoComplianceUsesConfiguredLag() => Assert.True(Batch400HaReadiness.RpoCompliant(10, 30));
    [Fact] public void B400_067_HaRtoReadyAcceptsSynchronizingReplica() => Assert.True(Batch400HaReadiness.RtoReady(new("SYNCHRONIZING", 10, 10, 5, true, false), true));
    [Fact] public void B400_068_HaQuorumRiskUsesMajority() { Assert.True(Batch400HaReadiness.QuorumRisk(1, 3)); Assert.False(Batch400HaReadiness.QuorumRisk(2, 3)); }
    [Fact] public void B400_069_HaSeverityUsesRiskBands() => Assert.Equal(B400Severity.Critical, Batch400HaReadiness.Severity(90));
    [Fact] public void B400_070_HaSummaryExplainsDegradation() { var result = Batch400HaReadiness.Summarize(new("NOT_SYNCHRONIZING", 1000, 1000, 300, true, false), false, 1, 3, 30); Assert.Equal(B400Severity.Critical, result.Severity); Assert.NotEmpty(result.Reason); }

    [Fact] public void B400_071_MaintenanceOperationNormalizationIsStrict() => Assert.Equal(MaintenanceOperation.IndexRebuild, Batch400MaintenanceSafety.NormalizeOperation("index rebuild"));
    [Fact] public void B400_072_MaintenanceBaseRiskEscalatesProduction() => Assert.Equal(MaintenanceRisk.Moderate, Batch400MaintenanceSafety.BaseRisk(MaintenanceOperation.Backup, true));
    [Fact] public void B400_073_MaintenanceApprovalRequiredForProduction() => Assert.True(Batch400MaintenanceSafety.ApprovalRequired(M(backup: true, approval: false)));
    [Fact] public void B400_074_MaintenanceRollbackRequiredForProduction() => Assert.True(Batch400MaintenanceSafety.RollbackRequired(M(backup: true, rollback: false)));
    [Fact] public void B400_075_MaintenanceWindowNotRequiredForBackup() => Assert.False(Batch400MaintenanceSafety.WindowRequired(M(backup: true)));
    [Fact] public void B400_076_MaintenanceBlockersAreDeterministic() { var blockers = Batch400MaintenanceSafety.Blockers(M(approval: false, rollback: false, critical: 1)); Assert.Contains("active-critical-incidents", blockers); Assert.Contains("approval-required", blockers); }
    [Fact] public void B400_077_MaintenanceAllowedRequiresNoBlockers() => Assert.True(Batch400MaintenanceSafety.Allowed(M(backup: true, approval: true, rollback: true, critical: 0)));
    [Fact] public void B400_078_MaintenanceScoreIsBounded() => Assert.InRange(Batch400MaintenanceSafety.Score(M(approval: false, rollback: false, critical: 2)), 0, 100);
    [Fact] public void B400_079_MaintenanceFingerprintIsStable() { var context = M(); Assert.Equal(Batch400MaintenanceSafety.Fingerprint(context), Batch400MaintenanceSafety.Fingerprint(context)); }
    [Fact] public void B400_080_MaintenanceDecisionCarriesSafeReason() { var decision = Batch400MaintenanceSafety.Decide(M(approval: false)); Assert.False(decision.Allowed); Assert.NotEmpty(decision.Reason); }

    [Fact] public void B400_081_FleetServerKeyNormalizationIsBounded() => Assert.Equal("SERVER-A", Batch400FleetCorrelation.NormalizeServerKey(" server-a "));
    [Fact] public void B400_082_FleetEnvironmentNormalizationIsStable() => Assert.Equal("PROD", Batch400FleetCorrelation.NormalizeEnvironment("prod"));
    [Fact] public void B400_083_FleetCorrelationWindowIsBounded() => Assert.Equal(TimeSpan.FromHours(24), Batch400FleetCorrelation.ClampWindow(TimeSpan.FromDays(3)));
    [Fact] public void B400_084_FleetBucketRoundsToWindow() { var at = DateTimeOffset.Parse("2026-08-11T06:07:00Z"); Assert.Equal(DateTimeOffset.Parse("2026-08-11T06:05:00Z"), Batch400FleetCorrelation.Bucket(at, TimeSpan.FromMinutes(5))); }
    [Fact] public void B400_085_FleetCorrelationKeyIsStable() { var signal = S("a", "prod", "R1", B400Severity.Warning, 0); Assert.Equal(Batch400FleetCorrelation.CorrelationKey(signal, TimeSpan.FromMinutes(5)), Batch400FleetCorrelation.CorrelationKey(signal, TimeSpan.FromMinutes(5))); }
    [Fact] public void B400_086_FleetSeverityWeightsCriticalHighest() => Assert.True(Batch400FleetCorrelation.SeverityWeight(B400Severity.Critical) > Batch400FleetCorrelation.SeverityWeight(B400Severity.Warning));
    [Fact] public void B400_087_FleetBlastRadiusCountsDistinctServers() => Assert.Equal(2, Batch400FleetCorrelation.BlastRadius([S("a", "prod", "R1", B400Severity.Warning, 0), S("a", "prod", "R1", B400Severity.Warning, 1), S("b", "prod", "R1", B400Severity.Warning, 1)]));
    [Fact] public void B400_088_FleetDominantRuleUsesCountThenName() => Assert.Equal("R1", Batch400FleetCorrelation.DominantRule([S("a", "prod", "R1", B400Severity.Warning, 0), S("b", "prod", "R1", B400Severity.Warning, 0), S("c", "prod", "R2", B400Severity.Warning, 0)]));
    [Fact] public void B400_089_FleetEnvironmentsAreDistinctAndSorted() => Assert.Equal(new[] { "DEV", "PROD" }, Batch400FleetCorrelation.Environments([S("a", "prod", "R1", B400Severity.Info, 0), S("b", "dev", "R1", B400Severity.Info, 0)]));
    [Fact] public void B400_090_FleetCorrelationProducesBoundedClusters() { var rows = Batch400FleetCorrelation.Correlate([S("a", "prod", "R1", B400Severity.Critical, 0), S("b", "prod", "R1", B400Severity.Warning, 1), S("c", "prod", "R2", B400Severity.Info, 0)], TimeSpan.FromMinutes(5), 1); Assert.Single(rows); Assert.Equal(B400Severity.Critical, rows[0].Severity); }

    [Fact] public void B400_091_TaskIdFormatterCoversOneHundredTasks() => Assert.Equal("B400-100", Batch400ReleaseGate.TaskId(100));
    [Fact] public void B400_092_TaskIdParserRejectsInvalidIds() { Assert.True(Batch400ReleaseGate.TryParseTaskId("B400-042", out var number)); Assert.Equal(42, number); Assert.False(Batch400ReleaseGate.TryParseTaskId("B400-000", out _)); }
    [Fact] public void B400_093_TaskCompletenessRequiresAllIds() => Assert.True(Batch400ReleaseGate.HasAllTasks(Enumerable.Range(1, 100).Select(Batch400ReleaseGate.TaskId)));
    [Fact] public void B400_094_ContractSchemaIsVersioned() => Assert.Equal("monitor-intelligence-b400-v1", Batch400ReleaseGate.SchemaVersion);
    [Fact] public void B400_095_FeatureGroupsAreDeterministic() { var groups = Batch400ReleaseGate.FeatureGroups(); Assert.Equal(10, groups.Count); Assert.Equal("wait-stat-intelligence", groups[0]); }
    [Fact] public void B400_096_GuardrailsKeepAutonomousExecutionDisabled() => Assert.Contains("no-autonomous-remediation", Batch400ReleaseGate.Guardrails());
    [Fact] public void B400_097_ContractManifestContainsOneHundredTasks() { var manifest = Batch400ReleaseGate.ContractManifest(); Assert.Equal(100, Assert.IsType<int>(manifest["taskCount"])); }
    [Fact] public void B400_098_ContractHashIsStableSha256() { var hash = Batch400ReleaseGate.ContractHash(); Assert.Equal(64, hash.Length); Assert.Equal(hash, Batch400ReleaseGate.ContractHash()); }
    [Fact] public void B400_099_ReleaseGateFailsClosedAndCanPass() { var ids = Enumerable.Range(1, 100).Select(Batch400ReleaseGate.TaskId).ToArray(); Assert.True(Batch400ReleaseGate.Evaluate(true, 495, 0, ids, true).Ready); Assert.False(Batch400ReleaseGate.Evaluate(false, 495, 0, ids, true).Ready); }
    [Fact] public void B400_100_ContractEndpointIsReadPolicyProtected() { var controller = typeof(Batch400IntelligenceController); var authorize = Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>()); Assert.Equal(MonitorPolicies.Read, authorize.Policy); var method = controller.GetMethod(nameof(Batch400IntelligenceController.Contract))!; var route = Assert.Single(method.GetCustomAttributes<HttpGetAttribute>()); Assert.Equal("/intelligence/v2/contract", route.Template); }

    private static QueryMetric Q(double duration, double cpu, double reads, string? plan = "A") => new("Q1", duration, cpu, reads, 10, plan);
    private static AgentJobRun Run(bool ok, int minute, int durationSeconds) => new(ok, Now.AddMinutes(minute), TimeSpan.FromSeconds(durationSeconds));
    private static MaintenanceContext M(bool backup = false, bool approval = true, bool rollback = true, int critical = 0) => new(backup ? MaintenanceOperation.Backup : MaintenanceOperation.Configuration, true, true, approval, rollback, critical, true, true);
    private static FleetSignal S(string server, string environment, string rule, B400Severity severity, int minute) => new(server, environment, rule, severity, Now.AddMinutes(minute));
}
