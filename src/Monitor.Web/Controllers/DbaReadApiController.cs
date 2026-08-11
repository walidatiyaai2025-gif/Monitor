using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Controllers;

public sealed record DbaApiPage(int Offset, int Limit, int Total);
public sealed record DbaFleetApiItem(Guid RegistrationId, string DisplayName, ServerEnvironmentClass Environment, string Freshness, int RiskScore, DbaRiskLevel RiskLevel);
public sealed record DbaRiskApiItem(Guid RegistrationId, int Score, DbaRiskLevel Level, bool Actionable, bool MaintenanceActive, bool AlertSuppressed);
public sealed record DbaIncidentPriorityApiItem(string IncidentId, Guid RegistrationId, string RuleId, string RuleFamily, FindingSeverity Severity, IncidentStatus Status, int Score, bool Actionable, string? Assignee);
public sealed record DbaFleetApiResponse(string SchemaVersion, DbaApiPage Page, IReadOnlyList<DbaFleetApiItem> Items);
public sealed record DbaRiskApiResponse(string SchemaVersion, DbaApiPage Page, IReadOnlyList<DbaRiskApiItem> Items);
public sealed record DbaIncidentPriorityApiResponse(string SchemaVersion, DbaApiPage Page, IReadOnlyList<DbaIncidentPriorityApiItem> Items);

public static class DbaReadApiContract
{
    public const string SchemaVersion = "monitor-dba-v1";
    public const int MaxPageSize = 100;

    public static (int Offset, int Limit) Page(int offset, int limit)
    {
        var boundedOffset = Math.Max(0, offset);
        var boundedLimit = limit <= 0 ? 50 : Math.Clamp(limit, 1, MaxPageSize);
        return (boundedOffset, boundedLimit);
    }

    public static string? NormalizeFilter(string? value, int maxLength = 60)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(character => char.IsControl(character) || !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
            throw new ArgumentException("Read API filter is invalid.", nameof(value));
        return normalized;
    }

    public static string ETag<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return '"' + Convert.ToHexString(SHA256.HashData(bytes)) + '"';
    }

    public static void ApplyHeaders(HttpResponse response, string etag)
    {
        response.Headers.ETag = etag;
        response.Headers.CacheControl = "private, max-age=15, must-revalidate";
        response.Headers["X-Content-Type-Options"] = "nosniff";
    }
}

[ApiController]
[Authorize(Policy = MonitorPolicies.Read)]
public sealed class DbaReadApiController(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    IHealthIncidentRepository incidents,
    IOperatorMetadataStore metadata,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet("/api/v1/dba/fleet")]
    public IActionResult Fleet(int offset = 0, int limit = 50, string? environment = null)
    {
        var envFilter = DbaReadApiContract.NormalizeFilter(environment, 32);
        ServerEnvironmentClass? parsedEnvironment = null;
        if (envFilter is not null)
        {
            if (!Enum.TryParse<ServerEnvironmentClass>(envFilter, true, out var parsed) || !Enum.IsDefined(parsed)) return BadRequest(new { message = "Environment filter is invalid." });
            parsedEnvironment = parsed;
        }
        var incidentSnapshot = incidents.GetAll();
        var now = timeProvider.GetUtcNow();
        var all = registrations.GetAll()
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(item =>
            {
                var operatorServer = metadata.GetServer(item.Id);
                var cached = cache.Peek(item.Id);
                var risk = DbaRiskScoring.Evaluate(item.Id, cached, incidentSnapshot, operatorServer, now);
                return new DbaFleetApiItem(item.Id, item.DisplayName, operatorServer.Environment, cached?.Freshness.ToString() ?? "Unavailable", risk.Score, risk.Level);
            })
            .Where(item => parsedEnvironment is null || item.Environment == parsedEnvironment)
            .ToArray();
        var (boundedOffset, boundedLimit) = DbaReadApiContract.Page(offset, limit);
        var response = new DbaFleetApiResponse(DbaReadApiContract.SchemaVersion, new(boundedOffset, boundedLimit, all.Length), all.Skip(boundedOffset).Take(boundedLimit).ToArray());
        return CacheAware(response);
    }

    [HttpGet("/api/v1/dba/risks")]
    public IActionResult Risks(int offset = 0, int limit = 50)
    {
        var rows = new DbaFleetRiskService(registrations, cache, incidents, metadata, timeProvider).Read()
            .Select(item => new DbaRiskApiItem(item.RegistrationId, item.Score, item.Level, item.Actionable, item.MaintenanceActive, item.AlertSuppressed))
            .ToArray();
        var (boundedOffset, boundedLimit) = DbaReadApiContract.Page(offset, limit);
        var response = new DbaRiskApiResponse(DbaReadApiContract.SchemaVersion, new(boundedOffset, boundedLimit, rows.Length), rows.Skip(boundedOffset).Take(boundedLimit).ToArray());
        return CacheAware(response);
    }

    [HttpGet("/api/v1/dba/incidents")]
    public IActionResult PriorityIncidents(int offset = 0, int limit = 50, string? assignee = null)
    {
        var normalizedAssignee = DbaReadApiContract.NormalizeFilter(assignee, EnterpriseOperatorValidation.MaxAssigneeLength);
        var all = new IncidentPriorityService(incidents, metadata, timeProvider).Queue(normalizedAssignee, 100)
            .Select(item => new DbaIncidentPriorityApiItem(item.Incident.Id, item.Incident.RegistrationId, item.Incident.RuleId, item.RuleFamily, item.Incident.Severity, item.Incident.Status, item.Score, item.Actionable, item.Assignee))
            .ToArray();
        var (boundedOffset, boundedLimit) = DbaReadApiContract.Page(offset, limit);
        var response = new DbaIncidentPriorityApiResponse(DbaReadApiContract.SchemaVersion, new(boundedOffset, boundedLimit, all.Length), all.Skip(boundedOffset).Take(boundedLimit).ToArray());
        return CacheAware(response);
    }

    private IActionResult CacheAware<T>(T response)
    {
        var etag = DbaReadApiContract.ETag(response);
        DbaReadApiContract.ApplyHeaders(Response, etag);
        if (Request.Headers.IfNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal))) return StatusCode(StatusCodes.Status304NotModified);
        return Ok(response);
    }
}
