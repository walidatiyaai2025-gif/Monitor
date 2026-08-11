using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

[Authorize(Policy = MonitorPolicies.Read)]
public sealed class DbaIntelligenceController : Controller
{
    private readonly IServerRegistrationRepository _registrations;
    private readonly IServerHealthSnapshotCache _cache;
    private readonly ISnapshotHistoryStore _history;
    private readonly IHealthIncidentRepository _incidents;
    private readonly IOperatorMetadataStore _operatorMetadata;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _configuration;

    public DbaIntelligenceController(
        IServerRegistrationRepository registrations,
        IServerHealthSnapshotCache cache,
        ISnapshotHistoryStore history,
        IHealthIncidentRepository incidents,
        IOperatorMetadataStore operatorMetadata,
        TimeProvider timeProvider,
        IConfiguration configuration)
    {
        _registrations = registrations;
        _cache = cache;
        _history = history;
        _incidents = incidents;
        _operatorMetadata = operatorMetadata;
        _timeProvider = timeProvider;
        _configuration = configuration;
    }

    [HttpGet("/enterprise/dba-intelligence")]
    public IActionResult Index()
    {
        var options = new DbaIntelligenceOptions
        {
            StorageCapacityBytes = Math.Max(0, _configuration.GetValue<long?>("DbaIntelligence:StorageCapacityBytes") ?? 0),
            HistoryHours = Math.Clamp(_configuration.GetValue<int?>("DbaIntelligence:HistoryHours") ?? 6, 1, 24),
            HistoryPoints = Math.Clamp(_configuration.GetValue<int?>("DbaIntelligence:HistoryPoints") ?? 48, 3, 288)
        };
        var model = new DbaIntelligenceDashboardService(
            _registrations,
            _cache,
            _history,
            _incidents,
            _operatorMetadata,
            _timeProvider,
            options).Read();
        return View(model);
    }
}
