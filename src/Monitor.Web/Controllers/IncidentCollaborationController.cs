using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class IncidentCollaborationController(
    IIncidentWorkflowService workflow,
    IHealthIncidentRepository incidents,
    IOperatorMetadataStore metadata,
    IAuditStore audit,
    TimeProvider timeProvider) : Controller
{
    [HttpPost("/alerts/{id}/resolve-with-note")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult ResolveWithNote(string id, string resolutionNote)
    {
        var incident = incidents.GetById(id);
        if (incident is null) return NotFound();
        var actor = Actor();
        try
        {
            _ = EnterpriseOperatorValidation.NormalizeNote(resolutionNote);
            if (!workflow.Resolve(id)) return Conflict(new { message = "Incident state changed or resolution is not allowed." });
            Collaboration().AddResolutionNote(id, actor, resolutionNote);
            audit.Append(actor, "incident.transition", id, $"{incident.Status}->Resolved");
            TempData["OperatorStatus"] = "Incident resolved with a bounded operator note.";
            return RedirectToAction("IncidentDetails", "Operations", new { id });
        }
        catch (ArgumentException exception)
        {
            TempData["OperatorError"] = SecurityInput.NormalizeAuditField(exception.Message, 180);
            return RedirectToAction("IncidentDetails", "Operations", new { id });
        }
    }

    [HttpPost("/alerts/{id}/reopen-with-reason")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = MonitorPolicies.Operate)]
    public IActionResult ReopenWithReason(string id, string reopenReason)
    {
        var incident = incidents.GetById(id);
        if (incident is null) return NotFound();
        var actor = Actor();
        try
        {
            _ = EnterpriseOperatorValidation.NormalizeNote(reopenReason);
            if (!workflow.Reopen(id)) return Conflict(new { message = "Incident state changed or reopen is not allowed." });
            Collaboration().AddReopenReason(id, actor, reopenReason);
            audit.Append(actor, "incident.transition", id, $"{incident.Status}->Open");
            TempData["OperatorStatus"] = "Incident reopened with a bounded reason.";
            return RedirectToAction("IncidentDetails", "Operations", new { id });
        }
        catch (ArgumentException exception)
        {
            TempData["OperatorError"] = SecurityInput.NormalizeAuditField(exception.Message, 180);
            return RedirectToAction("IncidentDetails", "Operations", new { id });
        }
    }

    private IIncidentCollaborationService Collaboration() => new IncidentCollaborationService(metadata, audit, timeProvider);

    private string Actor()
    {
        var actor = User.Identity?.Name;
        return string.IsNullOrWhiteSpace(actor) ? "unknown" : EnterpriseOperatorValidation.NormalizeActor(actor);
    }
}
