using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300ReleaseGateTests
{
    [Fact] public void B300_091_TaskId_FormatsCanonicalIds() => Assert.Equal("B300-100", Batch300ReleaseGate.TaskId(100));

    [Fact] public void B300_092_TryParseTaskId_RejectsOutOfRange()
    {
        Assert.True(Batch300ReleaseGate.TryParseTaskId("B300-001", out var number));
        Assert.Equal(1, number);
        Assert.False(Batch300ReleaseGate.TryParseTaskId("B300-101", out _));
    }

    [Fact] public void B300_093_HasCompleteTaskSet_RequiresAllHundred()
    {
        var complete = Enumerable.Range(1, 100).Select(Batch300ReleaseGate.TaskId).ToArray();
        Assert.True(Batch300ReleaseGate.HasCompleteTaskSet(complete));
        Assert.False(Batch300ReleaseGate.HasCompleteTaskSet(complete.Skip(1)));
    }

    [Fact] public void B300_094_IsCompatibleWithBatch200_RecognizesVerifiedStatus() => Assert.True(Batch300ReleaseGate.IsCompatibleWithBatch200("BATCH-200 100/100 COMPLETE"));

    [Fact] public void B300_095_ReadinessPercent_UsesPassedRatio()
    {
        var percent = Batch300ReleaseGate.ReadinessPercent([new("a", true, "ok"), new("b", false, "bad")]);
        Assert.Equal(50, percent);
    }

    [Fact] public void B300_096_GuardrailNoAutonomousRemediation_FailsWhenEnabled() => Assert.False(Batch300ReleaseGate.GuardrailNoAutonomousRemediation(true).Passed);

    [Fact] public void B300_097_GuardrailNoBrowserSql_FailsWhenEnabled() => Assert.False(Batch300ReleaseGate.GuardrailNoBrowserSql(true).Passed);

    [Fact] public void B300_098_GuardrailSecretsRedacted_FailsOnCanary() => Assert.False(Batch300ReleaseGate.GuardrailSecretsRedacted(true).Passed);

    [Fact] public void B300_099_Evaluate_RequiresEveryInvariant()
    {
        var result = Batch300ReleaseGate.Evaluate([new("a", true, "ok"), new("b", true, "ok")]);
        Assert.Equal(Batch300GateStatus.Ready, result.Status);
        Assert.Equal(2, result.Passed);
    }

    [Fact] public void B300_100_Controller_IsReadOnlyAuthorizedAndManifested()
    {
        var controllerType = typeof(Batch300IntelligenceController);
        var authorize = Assert.Single(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>());
        Assert.Equal(MonitorPolicies.Read, authorize.Policy);

        var method = controllerType.GetMethod(nameof(Batch300IntelligenceController.Contract));
        Assert.NotNull(method);
        var get = Assert.Single(method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true).Cast<HttpGetAttribute>());
        Assert.Equal("/intelligence/contract", get.Template);

        var manifest = Batch300ReleaseGate.ContractManifest();
        Assert.Equal(100, manifest["tasks"]);
        Assert.Equal(false, manifest["autonomousRemediation"]);
    }
}
