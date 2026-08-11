using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch400ReleaseGateTests
{
    [Fact] public void B400_101_TaskIdFormatterSupportsContinuationEnd() => Assert.Equal("B400-110", Batch400ReleaseGate.TaskId(110));
    [Fact] public void B400_102_TaskIdParserValidatesRange() { Assert.True(Batch400ReleaseGate.TryParseTaskId("B400-042", out var number)); Assert.Equal(42, number); Assert.True(Batch400ReleaseGate.TryParseTaskId("B400-110", out _)); Assert.False(Batch400ReleaseGate.TryParseTaskId("B400-000", out _)); }
    [Fact] public void B400_103_TaskCompletenessRequiresAllHundredAdditionalIds() => Assert.True(Batch400ReleaseGate.HasAllTasks(Enumerable.Range(11, 100).Select(Batch400ReleaseGate.TaskId)));
    [Fact] public void B400_104_ContractSchemaIsVersioned() => Assert.Equal("monitor-intelligence-b400-v1", Batch400ReleaseGate.SchemaVersion);
    [Fact] public void B400_105_FeatureGroupsAreDeterministic() { var groups = Batch400ReleaseGate.FeatureGroups(); Assert.Equal(10, groups.Count); Assert.Equal("wait-stat-intelligence", groups[0]); }
    [Fact] public void B400_106_GuardrailsKeepAutonomousExecutionDisabled() => Assert.Contains("no-autonomous-remediation", Batch400ReleaseGate.Guardrails());
    [Fact] public void B400_107_ContractManifestContainsHundredAdditionalTasks() { var manifest = Batch400ReleaseGate.ContractManifest(); Assert.Equal(100, Assert.IsType<int>(manifest["taskCount"])); Assert.Equal("B400-011", Assert.IsType<string>(manifest["rangeStart"])); Assert.Equal("B400-110", Assert.IsType<string>(manifest["rangeEnd"])); }
    [Fact] public void B400_108_ContractHashIsStableSha256() { var hash = Batch400ReleaseGate.ContractHash(); Assert.Equal(64, hash.Length); Assert.Equal(hash, Batch400ReleaseGate.ContractHash()); }
    [Fact] public void B400_109_ReleaseGateFailsClosedAndCanPass() { var ids = Enumerable.Range(11, 100).Select(Batch400ReleaseGate.TaskId).ToArray(); Assert.True(Batch400ReleaseGate.Evaluate(true, 498, 0, ids, true).Ready); Assert.False(Batch400ReleaseGate.Evaluate(false, 498, 0, ids, true).Ready); }
    [Fact] public void B400_110_ContractEndpointIsReadPolicyProtected() { var controller = typeof(Batch400IntelligenceController); var authorize = Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>()); Assert.Equal(MonitorPolicies.Read, authorize.Policy); var method = controller.GetMethod(nameof(Batch400IntelligenceController.Contract))!; var route = Assert.Single(method.GetCustomAttributes<HttpGetAttribute>()); Assert.Equal("/intelligence/v2/contract", route.Template); }
}
