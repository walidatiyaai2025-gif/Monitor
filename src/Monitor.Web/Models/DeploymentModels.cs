using Monitor.Web.Services;

namespace Monitor.Web.Models;

public sealed record DeploymentReadinessViewModel(
    DeploymentTopology Mode,
    bool Ready,
    string Status,
    string Message,
    IReadOnlyList<string> NodeLocalState)
{
    public static DeploymentReadinessViewModel SafeDefault() =>
        new(
            DeploymentTopology.SingleNode,
            Ready: true,
            Status: "Single-node ready",
            Message: "The current Monitor persistence and coordination implementation supports one active application instance.",
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
