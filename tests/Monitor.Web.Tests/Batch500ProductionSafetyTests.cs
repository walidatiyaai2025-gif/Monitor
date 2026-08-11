using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch500ProductionSafetyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact] public void B500_001_NormalizesProductionEnvironment() => Assert.Equal("production", Batch500DeploymentEvidence.NormalizeEnvironment(" PROD "));
    [Fact] public void B500_002_RequiresAbsoluteHttpsEvidenceUri() { Assert.True(Batch500DeploymentEvidence.IsHttpsUri("https://monitor.example/")); Assert.False(Batch500DeploymentEvidence.IsHttpsUri("http://monitor.example/")); }
    [Fact] public void B500_003_NormalizesArtifactFileName() => Assert.Equal("Monitor.zip", Batch500DeploymentEvidence.NormalizeArtifactName(@"C:\release\Monitor.zip"));
    [Fact] public void B500_004_ValidatesSha256Shape() { Assert.True(Batch500DeploymentEvidence.IsValidSha256(new string('a', 64))); Assert.False(Batch500DeploymentEvidence.IsValidSha256("abc")); }
    [Fact] public void B500_005_ValidatesCommitShaShape() { Assert.True(Batch500DeploymentEvidence.IsValidCommitSha("d512ee1")); Assert.False(Batch500DeploymentEvidence.IsValidCommitSha("xyz")); }
    [Fact] public void B500_006_ComputesNonNegativeEvidenceAge() { Assert.Equal(30d, Batch500DeploymentEvidence.AgeMinutes(Now, Now.AddMinutes(-30))); Assert.Equal(0d, Batch500DeploymentEvidence.AgeMinutes(Now, Now.AddMinutes(10))); }
    [Fact] public void B500_007_EnforcesEvidenceFreshness() { Assert.True(Batch500DeploymentEvidence.IsFresh(Now, Now.AddMinutes(-10), 15)); Assert.False(Batch500DeploymentEvidence.IsFresh(Now, Now.AddMinutes(-20), 15)); }
    [Fact] public void B500_008_FindsMissingRequiredEvidenceFields() { var e = new Dictionary<string, string?> { ["artifact"] = "x.zip", ["sha"] = "" }; Assert.Equal(["sha"], Batch500DeploymentEvidence.MissingFields(e, ["artifact", "sha"])); }
    [Fact] public void B500_009_SanitizesHostLabels() => Assert.Equal("monitor.example.internal", Batch500DeploymentEvidence.HostLabel(" Monitor.Example.Internal "));
    [Fact] public void B500_010_DeploymentFingerprintIsStableSha256() { var value = Batch500DeploymentEvidence.Fingerprint("prod", "a.zip", new string('a', 64), "d512ee1"); Assert.Equal(64, value.Length); Assert.Equal(value, Batch500DeploymentEvidence.Fingerprint("production", "a.zip", new string('A', 64), "d512ee1")); }

    [Fact] public void B500_011_NormalizesIisIdentityWithoutExpansion() => Assert.Equal(@"DOMAIN\svc-monitor", Batch500IisReadiness.NormalizeIdentity(@" DOMAIN\svc-monitor "));
    [Fact] public void B500_012_RequiresIntegratedPipeline() { Assert.True(Batch500IisReadiness.IsIntegratedPipeline("Integrated")); Assert.False(Batch500IisReadiness.IsIntegratedPipeline("Classic")); }
    [Fact] public void B500_013_RequiresNoManagedCode() { Assert.True(Batch500IisReadiness.IsNoManagedCode("No Managed Code")); Assert.False(Batch500IisReadiness.IsNoManagedCode("v4.0")); }
    [Fact] public void B500_014_RequiresAlwaysRunningStartMode() { Assert.True(Batch500IisReadiness.IsAlwaysRunning("AlwaysRunning")); Assert.False(Batch500IisReadiness.IsAlwaysRunning("OnDemand")); }
    [Fact] public void B500_015_RequiresPreloadEnabled() => Assert.True(Batch500IisReadiness.IsPreloadEnabled(true));
    [Fact] public void B500_016_Rejects32BitWorkerMode() { Assert.True(Batch500IisReadiness.Is64Bit(false)); Assert.False(Batch500IisReadiness.Is64Bit(true)); }
    [Fact] public void B500_017_RequiresIdleTimeoutDisabled() { Assert.True(Batch500IisReadiness.IsIdleTimeoutSafe(0)); Assert.False(Batch500IisReadiness.IsIdleTimeoutSafe(20)); }
    [Fact] public void B500_018_RequiresHttpsBinding() { Assert.True(Batch500IisReadiness.IsHttpsBinding("https", 443)); Assert.False(Batch500IisReadiness.IsHttpsBinding("http", 80)); }
    [Fact] public void B500_019_RequiresHostHeader() { Assert.True(Batch500IisReadiness.IsHostHeaderPresent("monitor.example.internal")); Assert.False(Batch500IisReadiness.IsHostHeaderPresent(" ")); }
    [Fact] public void B500_020_IisReadinessFailsClosedWithBlockers() { var blockers = Batch500IisReadiness.Blockers("", "Classic", "v4.0", "OnDemand", false, true, 20, "http", 80, ""); Assert.Equal(9, blockers.Count); }

    [Fact] public void B500_021_NormalizesCertificateHostname() => Assert.Equal("monitor.example.internal", Batch500CertificateReadiness.NormalizeHostname("Monitor.Example.Internal"));
    [Fact] public void B500_022_ComputesCertificateRemainingDays() => Assert.Equal(40, Batch500CertificateReadiness.RemainingDays(Now, Now.AddDays(40.8)));
    [Fact] public void B500_023_BandsCertificateExpiryRisk() { Assert.Equal("Expired", Batch500CertificateReadiness.ExpiryRisk(Now, Now.AddMinutes(-1))); Assert.Equal("Warning", Batch500CertificateReadiness.ExpiryRisk(Now, Now.AddDays(20))); Assert.Equal("Healthy", Batch500CertificateReadiness.ExpiryRisk(Now, Now.AddDays(60))); }
    [Fact] public void B500_024_RequiresStrongRsaKey() { Assert.True(Batch500CertificateReadiness.IsStrongRsaKey(2048)); Assert.False(Batch500CertificateReadiness.IsStrongRsaKey(1024)); }
    [Fact] public void B500_025_RejectsWeakSignatureAlgorithms() { Assert.True(Batch500CertificateReadiness.IsAllowedSignature("sha256RSA")); Assert.False(Batch500CertificateReadiness.IsAllowedSignature("sha1RSA")); }
    [Fact] public void B500_026_MatchesExactAndWildcardSan() { Assert.True(Batch500CertificateReadiness.SanMatches("monitor.example.com", ["monitor.example.com"])); Assert.True(Batch500CertificateReadiness.SanMatches("monitor.example.com", ["*.example.com"])); Assert.False(Batch500CertificateReadiness.SanMatches("deep.monitor.example.com", ["*.example.com"])); }
    [Fact] public void B500_027_NormalizesCertificateThumbprint() => Assert.Equal("AABBCC", Batch500CertificateReadiness.NormalizeThumbprint("aa bb:cc"));
    [Fact] public void B500_028_RequiresHealthyCertificateChain() { Assert.True(Batch500CertificateReadiness.IsChainHealthy(true, 0)); Assert.False(Batch500CertificateReadiness.IsChainHealthy(false, 0)); }
    [Fact] public void B500_029_CertificateRiskScoreIsBounded() => Assert.InRange(Batch500CertificateReadiness.RiskScore(Now, Now.AddDays(-1), 1024, "sha1", false, false, 2), 0, 100);
    [Fact] public void B500_030_CertificateReadinessIsFailClosed() { Assert.True(Batch500CertificateReadiness.IsCertificateReady(Now, Now.AddDays(90), 2048, "sha256RSA", true, true, 0)); Assert.False(Batch500CertificateReadiness.IsCertificateReady(Now, Now.AddDays(2), 2048, "sha256RSA", true, true, 0)); }

    [Fact] public void B500_031_StatePathMustBeOutsideReleaseRoot() { Assert.True(Batch500Durability.IsExternalStatePath(@"C:\MonitorState", @"D:\MonitorState")); Assert.False(Batch500Durability.IsExternalStatePath(@"C:\Releases\rc20", @"C:\Releases\rc20\App_Data")); }
    [Fact] public void B500_032_KeyRingPathMustBeOutsideReleaseRoot() => Assert.True(Batch500Durability.IsExternalKeyRingPath("/srv/releases/rc20", "/srv/monitor-state/keyring"));
    [Fact] public void B500_033_RegistrationCountCannotRegress() { Assert.True(Batch500Durability.RegistrationCountPreserved(3, 3)); Assert.False(Batch500Durability.RegistrationCountPreserved(3, 2)); }
    [Fact] public void B500_034_SnapshotEvidenceCountCannotRegress() { Assert.True(Batch500Durability.SnapshotEvidencePreserved(2, 4)); Assert.False(Batch500Durability.SnapshotEvidencePreserved(4, 2)); }
    [Fact] public void B500_035_AuditSequenceMustBeMonotonic() { Assert.True(Batch500Durability.AuditMonotonic(100, 101)); Assert.False(Batch500Durability.AuditMonotonic(100, 99)); }
    [Fact] public void B500_036_IncidentSequenceMustBeMonotonic() { Assert.True(Batch500Durability.IncidentMonotonic(5, 5)); Assert.False(Batch500Durability.IncidentMonotonic(5, 4)); }
    [Fact] public void B500_037_ProtectedCredentialMustResolveAfterRestart() => Assert.True(Batch500Durability.CredentialResolved(true));
    [Fact] public void B500_038_HealthMustRecoverAfterRestart() { Assert.True(Batch500Durability.HealthRecovered(true, true)); Assert.False(Batch500Durability.HealthRecovered(true, false)); }
    [Fact] public void B500_039_RestartMustMeetBoundedSla() { Assert.True(Batch500Durability.RestartWithinSla(TimeSpan.FromSeconds(15), 30)); Assert.False(Batch500Durability.RestartWithinSla(TimeSpan.FromSeconds(45), 30)); }
    [Fact] public void B500_040_DurabilityEvaluationEnumeratesBlockers() { var blockers = Batch500Durability.Evaluate(false, false, false, false, false, false, false, false, false); Assert.Equal(9, blockers.Count); }

    [Fact] public void B500_041_BackupFreshnessIsBounded() { Assert.True(Batch500RollbackSafety.IsBackupFresh(Now, Now.AddHours(-1), 2)); Assert.False(Batch500RollbackSafety.IsBackupFresh(Now, Now.AddHours(-3), 2)); }
    [Fact] public void B500_042_BackupChecksumMustBeSha256() => Assert.True(Batch500RollbackSafety.ChecksumPresent(new string('b', 64)));
    [Fact] public void B500_043_BackupManifestMustExist() => Assert.True(Batch500RollbackSafety.ManifestPresent(true));
    [Fact] public void B500_044_PreviousReleaseMustBePreserved() => Assert.True(Batch500RollbackSafety.PreviousReleasePreserved(true));
    [Fact] public void B500_045_DurableStateMustBeBackedUp() => Assert.True(Batch500RollbackSafety.DurableStateIncluded(true));
    [Fact] public void B500_046_KeyRingMustBeBackedUp() => Assert.True(Batch500RollbackSafety.KeyRingIncluded(true));
    [Fact] public void B500_047_RestoreValidationMustPass() => Assert.True(Batch500RollbackSafety.RestoreValidationPassed(true));
    [Fact] public void B500_048_RollbackSmokeMustPass() => Assert.True(Batch500RollbackSafety.RollbackSmokePassed(true));
    [Fact] public void B500_049_RollbackMustMeetSla() { Assert.True(Batch500RollbackSafety.RollbackWithinSla(TimeSpan.FromMinutes(10), 15)); Assert.False(Batch500RollbackSafety.RollbackWithinSla(TimeSpan.FromMinutes(20), 15)); }
    [Fact] public void B500_050_RollbackEvaluationFailsClosed() { var blockers = Batch500RollbackSafety.Evaluate(false, false, false, false, false, false, false, false, false); Assert.Equal(9, blockers.Count); }

    [Fact] public void B500_051_MonitoredLoginMustNotBeSysadmin() { Assert.True(Batch500LeastPrivilege.IsNonSysAdmin(false)); Assert.False(Batch500LeastPrivilege.IsNonSysAdmin(true)); }
    [Fact] public void B500_052_ServerStateReadPermissionIsRequired() => Assert.True(Batch500LeastPrivilege.HasServerStateRead(true));
    [Fact] public void B500_053_ViewAnyDatabasePermissionIsRequired() => Assert.True(Batch500LeastPrivilege.HasViewAnyDatabase(true));
    [Fact] public void B500_054_DefinitionMetadataPermissionIsRequired() => Assert.True(Batch500LeastPrivilege.HasDefinitionMetadata(true));
    [Fact] public void B500_055_AgentMetadataReadPermissionIsRequired() => Assert.True(Batch500LeastPrivilege.HasAgentMetadataRead(true));
    [Fact] public void B500_056_TargetDmlMustRemainAbsent() { Assert.True(Batch500LeastPrivilege.NoTargetDml(false)); Assert.False(Batch500LeastPrivilege.NoTargetDml(true)); }
    [Fact] public void B500_057_TargetDdlMustRemainAbsent() { Assert.True(Batch500LeastPrivilege.NoTargetDdl(false)); Assert.False(Batch500LeastPrivilege.NoTargetDdl(true)); }
    [Fact] public void B500_058_ImpersonationMustRemainAbsent() { Assert.True(Batch500LeastPrivilege.NoImpersonation(false)); Assert.False(Batch500LeastPrivilege.NoImpersonation(true)); }
    [Fact] public void B500_059_CollectionMustSucceedWithLeastPrivilege() => Assert.True(Batch500LeastPrivilege.CollectionSucceeded(true));
    [Fact] public void B500_060_LeastPrivilegeEvaluationFailsClosed() { var blockers = Batch500LeastPrivilege.Evaluate(false, false, false, false, false, false, false, false, false); Assert.Equal(9, blockers.Count); }

    [Fact] public void B500_061_NormalizesBoundedHealthStates() { Assert.Equal("Ready", Batch500ProductionSmoke.NormalizeHealthStatus(" ready ")); Assert.Equal("Unknown", Batch500ProductionSmoke.NormalizeHealthStatus("anything")); }
    [Fact] public void B500_062_LiveEndpointRequiresSuccessStatus() { Assert.True(Batch500ProductionSmoke.LivePassed(200, "Live")); Assert.False(Batch500ProductionSmoke.LivePassed(503, "Live")); }
    [Fact] public void B500_063_ReadinessEndpointRequiresReadyState() { Assert.True(Batch500ProductionSmoke.ReadyPassed(200, "Ready")); Assert.False(Batch500ProductionSmoke.ReadyPassed(200, "Degraded")); }
    [Fact] public void B500_064_AggregateHealthEndpointRequiresBoundedHealthyState() { Assert.True(Batch500ProductionSmoke.HealthPassed(200, "Ready")); Assert.False(Batch500ProductionSmoke.HealthPassed(500, "Ready")); }
    [Fact] public void B500_065_AdministratorLoginMustEstablishAuthentication() => Assert.True(Batch500ProductionSmoke.LoginPassed(true));
    [Fact] public void B500_066_ProtectedRouteMustReturnSuccess() { Assert.True(Batch500ProductionSmoke.ProtectedRoutePassed(200)); Assert.False(Batch500ProductionSmoke.ProtectedRoutePassed(302)); }
    [Fact] public void B500_067_AntiforgeryMustRemainEnforced() => Assert.True(Batch500ProductionSmoke.AntiforgeryEnforced(true));
    [Fact] public void B500_068_AuthenticationCookieMustRemainSecure() => Assert.True(Batch500ProductionSmoke.SecureCookieEnforced(true));
    [Fact] public void B500_069_ProductionSmokeMustUseHttps() { Assert.True(Batch500ProductionSmoke.HttpsOnly("https://monitor.example/")); Assert.False(Batch500ProductionSmoke.HttpsOnly("http://monitor.example/")); }
    [Fact] public void B500_070_ProductionSmokeEvaluationFailsClosed() { var blockers = Batch500ProductionSmoke.Evaluate(false, false, false, false, false, false, false, false); Assert.Equal(8, blockers.Count); }

    [Fact] public void B500_071_ComputesCutoverWindowDuration() => Assert.Equal(45d, Batch500CutoverSafety.WindowDurationMinutes(Now, Now.AddMinutes(45)));
    [Fact] public void B500_072_ValidatesBoundedCutoverWindow() { Assert.True(Batch500CutoverSafety.WindowValid(Now, Now.AddMinutes(30), 60)); Assert.False(Batch500CutoverSafety.WindowValid(Now, Now.AddMinutes(90), 60)); }
    [Fact] public void B500_073_NormalizesChangeTicket() => Assert.Equal("CHG-12345", Batch500CutoverSafety.NormalizeTicket(" chg-12345 "));
    [Fact] public void B500_074_RequiresStructuredChangeTicket() { Assert.True(Batch500CutoverSafety.ValidTicket("CHG-123")); Assert.False(Batch500CutoverSafety.ValidTicket("123")); }
    [Fact] public void B500_075_RequiresEnoughApprovals() { Assert.True(Batch500CutoverSafety.ApprovalCountEnough(2, 2)); Assert.False(Batch500CutoverSafety.ApprovalCountEnough(1, 2)); }
    [Fact] public void B500_076_RequiresRollbackOwner() { Assert.True(Batch500CutoverSafety.RollbackOwnerPresent("operator")); Assert.False(Batch500CutoverSafety.RollbackOwnerPresent(" ")); }
    [Fact] public void B500_077_RejectsChangeFreezeConflict() { Assert.True(Batch500CutoverSafety.NoFreezeConflict(false)); Assert.False(Batch500CutoverSafety.NoFreezeConflict(true)); }
    [Fact] public void B500_078_BackupGateMustPassBeforeCutover() => Assert.True(Batch500CutoverSafety.BackupGatePassed(true));
    [Fact] public void B500_079_CutoverBlockersAreDeterministic() { var blockers = Batch500CutoverSafety.Blockers(false, false, false, false, false, false); Assert.Equal(["change-window-invalid", "change-ticket-invalid", "approvals-missing", "rollback-owner-missing", "change-freeze-conflict", "backup-gate-failed"], blockers); }
    [Fact] public void B500_080_GoNoGoRequiresZeroBlockers() { Assert.True(Batch500CutoverSafety.GoNoGo([])); Assert.False(Batch500CutoverSafety.GoNoGo(["x"])); }

    [Fact] public void B500_081_DetectsPasswordAssignments() { Assert.True(Batch500EvidenceSafety.ContainsPasswordAssignment("Password=secret")); Assert.False(Batch500EvidenceSafety.ContainsPasswordAssignment("credentialStatus=protected")); }
    [Fact] public void B500_082_DetectsConnectionStringShapes() { Assert.True(Batch500EvidenceSafety.ContainsConnectionStringShape("Server=db;User Id=u;Password=p;")); Assert.False(Batch500EvidenceSafety.ContainsConnectionStringShape("server=db01")); }
    [Fact] public void B500_083_DetectsRawProviderErrors() { Assert.True(Batch500EvidenceSafety.ContainsRawProviderError("Microsoft.Data.SqlClient.SqlException")); Assert.False(Batch500EvidenceSafety.ContainsRawProviderError("authentication failed")); }
    [Fact] public void B500_084_DetectsArbitrarySqlText() { Assert.True(Batch500EvidenceSafety.ContainsSqlText("SELECT * FROM sys.databases")); Assert.False(Batch500EvidenceSafety.ContainsSqlText("database metadata unavailable")); }
    [Fact] public void B500_085_NormalizesEvidenceKeys() => Assert.Equal("artifact.sha_256", Batch500EvidenceSafety.NormalizeKey(" Artifact.SHA_256 "));
    [Fact] public void B500_086_ClampsEvidenceValuesAndRemovesNewlines() => Assert.Equal("abc def", Batch500EvidenceSafety.ClampValue("abc\r\ndef"));
    [Fact] public void B500_087_ProducesOpaqueEvidenceIdentifiers() { var id = Batch500EvidenceSafety.OpaqueId("registration-123"); Assert.Equal(16, id.Length); Assert.Equal(id, Batch500EvidenceSafety.OpaqueId("registration-123")); }
    [Fact] public void B500_088_SanitizesEvidenceHost() => Assert.Equal("db01.internal", Batch500EvidenceSafety.SafeHost(" DB01.INTERNAL "));
    [Fact] public void B500_089_ExportsOnlyAllowlistedEvidenceFields() { var input = new Dictionary<string, string?> { ["artifact"] = "a.zip", ["password"] = "x" }; var output = Batch500EvidenceSafety.FilterAllowedFields(input, ["artifact"]); Assert.Single(output); Assert.Equal("a.zip", output["artifact"]); }
    [Fact] public void B500_090_EvidenceSafetyFailsClosedForSensitiveShapes() { Assert.True(Batch500EvidenceSafety.IsSafeEvidence("artifact checksum verified")); Assert.False(Batch500EvidenceSafety.IsSafeEvidence("Server=db;User Id=u;Password=p;")); }

    [Fact] public void B500_091_TaskIdFormatterCoversFullBatch() => Assert.Equal("B500-100", Batch500ReleaseGate.TaskId(100));
    [Fact] public void B500_092_TaskIdParserRejectsOutOfRangeValues() { Assert.True(Batch500ReleaseGate.TryParseTaskId("B500-042", out var number)); Assert.Equal(42, number); Assert.False(Batch500ReleaseGate.TryParseTaskId("B500-101", out _)); }
    [Fact] public void B500_093_TaskCompletenessRequiresAllHundredIds() => Assert.True(Batch500ReleaseGate.HasAllTasks(Enumerable.Range(1, 100).Select(Batch500ReleaseGate.TaskId)));
    [Fact] public void B500_094_ReleaseSchemaIsVersioned() => Assert.Equal("monitor-production-safety-b500-v1", Batch500ReleaseGate.SchemaVersion);
    [Fact] public void B500_095_FeatureGroupsAreDeterministic() { var groups = Batch500ReleaseGate.FeatureGroups(); Assert.Equal(10, groups.Count); Assert.Equal("deployment-evidence", groups[0]); Assert.Equal("release-contract", groups[^1]); }
    [Fact] public void B500_096_GuardrailsKeepExternalAcceptanceExplicit() { Assert.Contains("external-iis-acceptance-remains-required", Batch500ReleaseGate.Guardrails()); Assert.Contains("no-browser-to-sql", Batch500ReleaseGate.Guardrails()); }
    [Fact] public void B500_097_ContractManifestContainsOneHundredTasks() { var manifest = Batch500ReleaseGate.ContractManifest(); Assert.Equal(100, Assert.IsType<int>(manifest["taskCount"])); Assert.Equal("B500-001", Assert.IsType<string>(manifest["rangeStart"])); Assert.Equal("B500-100", Assert.IsType<string>(manifest["rangeEnd"])); Assert.Equal("required", Assert.IsType<string>(manifest["externalAcceptance"])); }
    [Fact] public void B500_098_ContractHashIsStableSha256() { var hash = Batch500ReleaseGate.ContractHash(); Assert.Equal(64, hash.Length); Assert.Equal(hash, Batch500ReleaseGate.ContractHash()); }
    [Fact] public void B500_099_ReleaseGatePassesRepositoryEvidenceButRejectsFalseExternalClaim() { var ids = Enumerable.Range(1, 100).Select(Batch500ReleaseGate.TaskId).ToArray(); Assert.True(Batch500ReleaseGate.Evaluate(true, 631, 0, ids, true, false).Ready); Assert.False(Batch500ReleaseGate.Evaluate(true, 631, 0, ids, true, true).Ready); }
    [Fact] public void B500_100_ContractEndpointIsReadPolicyProtected() { var controller = typeof(Batch500ProductionController); var authorize = Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>()); Assert.Equal(MonitorPolicies.Read, authorize.Policy); var method = controller.GetMethod(nameof(Batch500ProductionController.Contract))!; var route = Assert.Single(method.GetCustomAttributes<HttpGetAttribute>()); Assert.Equal("/production/v1/acceptance-contract", route.Template); }
}
