using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum DeploymentTopology
{
    SingleNode,
    MultiNode
}

public sealed class DeploymentTopologyOptions
{
    public const string SectionName = "Deployment";

    public DeploymentTopology Mode { get; set; } = DeploymentTopology.SingleNode;

    public void Validate()
    {
        if (!Enum.IsDefined(Mode))
        {
            throw new InvalidOperationException("Deployment:Mode is not supported.");
        }

        if (Mode == DeploymentTopology.MultiNode)
        {
            throw new InvalidOperationException(
                "Deployment:Mode MultiNode requires shared registration, operational-state and coordination providers. " +
                "The current Monitor persistence and coordination implementations are single-node only.");
        }
    }

    public DeploymentReadinessViewModel ToReadiness() =>
        new(
            Mode,
            Ready: Mode == DeploymentTopology.SingleNode,
            Status: "Single-node ready",
            Message: "Local durable stores are safe for one active Monitor application instance. Multi-node startup is blocked until shared state and distributed coordination are implemented.",
            NodeLocalState:
            [
                "Registration metadata store",
                "Audit, history and incident operational stores",
                "Protected local SQL credential store and key ring",
                "Login attempt limiter",
                "Snapshot cache and single-flight gates",
                "Scheduler ownership, backoff and runtime status"
            ]);
}
