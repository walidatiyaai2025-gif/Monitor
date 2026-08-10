using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DeploymentTopologyTests
{
    [Fact]
    public void DefaultTopology_IsSingleNodeAndValid()
    {
        var options = new DeploymentTopologyOptions();

        options.Validate();
        var readiness = options.ToReadiness();

        Assert.Equal(DeploymentTopology.SingleNode, options.Mode);
        Assert.True(readiness.Ready);
        Assert.Equal("Single-node ready", readiness.Status);
        Assert.Contains("Snapshot cache and single-flight gates", readiness.NodeLocalState);
        Assert.Contains("Protected local SQL credential store and key ring", readiness.NodeLocalState);
    }

    [Fact]
    public void ExplicitSingleNode_IsAccepted()
    {
        var options = new DeploymentTopologyOptions { Mode = DeploymentTopology.SingleNode };

        var exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void MultiNode_FailsClosedUntilSharedStateAndCoordinationExist()
    {
        var options = new DeploymentTopologyOptions { Mode = DeploymentTopology.MultiNode };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("MultiNode", exception.Message, StringComparison.Ordinal);
        Assert.Contains("shared", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coordination", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection string", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UndefinedTopologyValue_FailsClosed()
    {
        var options = new DeploymentTopologyOptions { Mode = (DeploymentTopology)999 };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Deployment:Mode is not supported.", exception.Message);
    }

    [Fact]
    public void ReadinessProjection_IsBoundedAndContainsNoRuntimeValues()
    {
        var readiness = new DeploymentTopologyOptions().ToReadiness();
        var text = string.Join("|", readiness.NodeLocalState) + "|" + readiness.Message;

        Assert.True(readiness.NodeLocalState.Count <= 10);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection string", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sql01", text, StringComparison.OrdinalIgnoreCase);
    }
}
