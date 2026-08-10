using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Manage)]
public sealed class OperationalBackupController(IOperationalBackupService backups, IAuditStore audit) : Controller
{
    [HttpPost("/settings/backups/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBackup(CancellationToken cancellationToken)
    {
        var actor = Actor();
        if (actor is null) return Forbid();
        try
        {
            var backup = await backups.CreateAsync(cancellationToken);
            audit.Append(actor, "backup.create", backup.BackupId, "created");
            TempData["BackupStatus"] = "Operational backup created and checksum manifest verified.";
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or InvalidDataException)
        {
            audit.Append(actor, "backup.create", "operational", "failed");
            TempData["BackupStatus"] = "Operational backup could not be created safely.";
        }
        return RedirectToAction("Settings", "Operations");
    }

    [HttpPost("/settings/backups/{backupId}/validate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidateBackup(string backupId, CancellationToken cancellationToken)
    {
        var actor = Actor();
        if (actor is null) return Forbid();
        var result = await backups.ValidateAsync(backupId, cancellationToken);
        audit.Append(actor, "backup.validate", "operational", result.IsValid ? "valid" : "invalid");
        TempData["BackupStatus"] = result.Message;
        return RedirectToAction("Settings", "Operations");
    }

    [HttpPost("/settings/backups/{backupId}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreBackup(string backupId, string? confirmation, CancellationToken cancellationToken)
    {
        var actor = Actor();
        if (actor is null) return Forbid();
        if (!string.Equals(confirmation?.Trim(), "RESTORE", StringComparison.Ordinal))
        {
            audit.Append(actor, "backup.restore", "operational", "confirmation-rejected");
            TempData["BackupStatus"] = "Type RESTORE exactly to confirm an operational-state restore.";
            return RedirectToAction("Settings", "Operations");
        }

        var result = await backups.RestoreAsync(backupId, cancellationToken);
        audit.Append(actor, "backup.restore", "operational", result.Status.ToString());
        TempData["BackupStatus"] = result.Message;
        return RedirectToAction("Settings", "Operations");
    }

    private string? Actor()
    {
        var actor = User.Identity?.Name?.Trim();
        return string.IsNullOrWhiteSpace(actor) ? null : actor;
    }
}
