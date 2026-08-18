using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class StorageHealthReportsController : Controller
{
    private readonly StorageHealthReportingService _report;
    private readonly TimeProvider _timeProvider;

    public StorageHealthReportsController(
        IServerRegistrationRepository registrations,
        IServerHealthSnapshotCache cache,
        TimeProvider timeProvider)
    {
        _report = new StorageHealthReportingService(registrations, cache);
        _timeProvider = timeProvider;
    }

    [HttpGet("/reports/storage-health.csv")]
    public IActionResult StorageHealth()
    {
        EnterpriseSecurityPolicy.ApplySecureDownloadHeaders(Response);
        var fileName = EnterpriseSecurityPolicy.SafeDownloadFileName(
            EnterpriseDownloadSubject.StorageHealth,
            _timeProvider.GetUtcNow(),
            "csv");
        return File(_report.Build(), "text/csv; charset=utf-8", fileName);
    }
}
