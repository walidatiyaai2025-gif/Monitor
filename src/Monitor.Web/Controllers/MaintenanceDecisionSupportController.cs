using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

public sealed record MaintenanceDecisionSupportPageViewModel(
    ServerRegistration Registration,
    ServerOperatorMetadata Metadata,
    bool MaintenanceWindowActive,
    MaintenanceOperation Operation,
    MaintenanceDecisionSupportResult Result,
    int ActiveCriticalIncidents);

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class MaintenanceDecisionSupportController(
    IServerRegistrationRepository registrations,
    IOperatorMetadataStore operatorMetadata,
    IHealthIncidentRepository incidents,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("/enterprise/maintenance/{id:guid}")]
    public IActionResult Index(Guid id, string? operation = null)
    {
        var registration = registrations.GetById(id);
        if (registration is null || !registration.IsEnabled) return NotFound();

        var metadata = operatorMetadata.GetServer(id);
        var activeCriticalIncidents = incidents.GetAll().Count(incident =>
            incident.RegistrationId == id &&
            incident.Status != IncidentStatus.Resolved &&
            incident.Severity == FindingSeverity.Critical);
        var selectedOperation = MaintenanceDecisionSupport.NormalizeOperation(operation);
        var evidence = new MaintenanceDecisionEvidence(
            selectedOperation,
            metadata.Environment == ServerEnvironmentClass.Production,
            EnterpriseOperatorPolicy.IsMaintenanceActive(metadata, timeProvider.GetUtcNow()),
            InApprovedWindow: null,
            HasApproval: null,
            HasRollbackPlan: null,
            ActiveCriticalIncidents: activeCriticalIncidents,
            ReplicaHealthy: null,
            RecentBackupAvailable: null);
        var result = MaintenanceDecisionSupport.Evaluate(evidence);

        return View(new MaintenanceDecisionSupportPageViewModel(
            registration,
            metadata,
            evidence.ObservedMaintenanceWindowActive,
            selectedOperation,
            result,
            activeCriticalIncidents));
    }
}
