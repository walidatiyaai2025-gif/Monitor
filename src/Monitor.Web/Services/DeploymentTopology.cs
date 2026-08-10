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
    }

    public DeploymentReadinessViewModel ToReadiness() =>
        Mode == DeploymentTopology.SingleNode
            ? new(
                Mode,
                Ready: true,
                Status: "Single-node ready",
                Message: "The current Monitor deployment is configured for one active application instance.",
                NodeLocalState:
                [
                    "Registration metadata store",
                    "Audit, history and incident operational stores",
                    "Protected local SQL credential store and key ring",
                    "Login attempt limiter",
                    "Snapshot cache and single-flight gates",
                    "Scheduler ownership, backoff and runtime status"
                ])
            : new(
                Mode,
                Ready: false,
                Status: "Multi-node prerequisites pending",
                Message: "Multi-node readiness must be evaluated against the selected shared-state and coordination configuration.",
                NodeLocalState: []);
}
