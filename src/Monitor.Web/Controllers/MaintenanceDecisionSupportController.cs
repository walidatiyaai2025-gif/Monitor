using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

public sealed record MaintenanceDecisionSupportPageViewModel(
    ServerRegistration Registration,
    ServerOperatorPolicyState Policy,
    bool? MaintenanceWindowActive,
    MaintenanceOperation Operation,
    MaintenanceDecisionSupportResult Result,
    int? ActiveCriticalIncidents,
    bool IncidentEvidenceComplete,
    int IncidentEvidenceLimit);

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class MaintenanceDecisionSupportController(
    IServerRegistrationRepository registrations,
    IOperatorPolicyReadService operatorPolicy,
    IHealthIncidentRepository incidents) : Controller
{
    [HttpGet("/enterprise/maintenance/{id:guid}")]
    public IActionResult Index(Guid id, string? operation = null)
    {
        var registration = registrations.GetById(id);
        if (registration is null || !registration.IsEnabled) return NotFound();

        var policy = operatorPolicy.GetServer(id);
        var incidentRead = BoundedIncidentReadModel.ActiveForServer(incidents, id);
        int? activeCriticalIncidents = incidentRead.IsComplete
            ? incidentRead.Incidents.Count(incident => incident.Severity == FindingSeverity.Critical)
            : null;
        var selectedOperation = MaintenanceDecisionSupport.NormalizeOperation(operation);
        var evidence = new MaintenanceDecisionEvidence(
            selectedOperation,
            IsProduction: policy.PolicyReadable ? policy.Environment == ServerEnvironmentClass.Production : null,
            ObservedMaintenanceWindowActive: policy.PolicyReadable ? policy.MaintenanceActive : null,
            InApprovedWindow: null,
            HasApproval: null,
            HasRollbackPlan: null,
            ActiveCriticalIncidents: activeCriticalIncidents,
            ReplicaHealthy: null,
            RecentBackupAvailable: null);
        var result = MaintenanceDecisionSupport.Evaluate(evidence);

        return View(new MaintenanceDecisionSupportPageViewModel(
            registration,
            policy,
            evidence.ObservedMaintenanceWindowActive,
            selectedOperation,
            result,
            activeCriticalIncidents,
            incidentRead.IsComplete,
            incidentRead.Limit));
    }
}
