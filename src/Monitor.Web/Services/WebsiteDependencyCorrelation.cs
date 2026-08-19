using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record WebsiteDependencySignal(
    Guid RegistrationId,
    string RegistrationName,
    string RuleId,
    FindingSeverity Severity,
    DateTimeOffset ObservedAtUtc,
    string Summary);

public sealed record WebsiteCorrelationAssessment(
    bool HasConfiguredDependencies,
    string Confidence,
    string Summary,
    IReadOnlyList<WebsiteDependencySignal> Signals)
{
    public static WebsiteCorrelationAssessment None { get; } =
        new(false, "none", "No server/database dependencies are linked to this website target.", Array.Empty<WebsiteDependencySignal>());
}

public interface IWebsiteDependencyCorrelationService
{
    WebsiteCorrelationAssessment Assess(WebsiteTargetDefinition target, WebsiteProbeHistoryPoint? websiteEvidence);
}

public sealed class WebsiteDependencyCorrelationService(
    IServerRegistrationRepository registrations,
    IHealthIncidentRepository incidents) : IWebsiteDependencyCorrelationService
{
    private static readonly TimeSpan CorrelationWindow = TimeSpan.FromMinutes(10);
    private const int MaxSignals = 10;

    public WebsiteCorrelationAssessment Assess(WebsiteTargetDefinition target, WebsiteProbeHistoryPoint? websiteEvidence)
    {
        ArgumentNullException.ThrowIfNull(target);
        var linkedIds = (target.LinkedRegistrationIds ?? Array.Empty<Guid>()).Distinct().Take(WebsiteTargetValidator.MaxLinkedRegistrations).ToArray();
        if (linkedIds.Length == 0) return WebsiteCorrelationAssessment.None;
        if (websiteEvidence is null)
            return new(true, "none", "Dependencies are linked, but there is no website probe evidence to correlate yet.", Array.Empty<WebsiteDependencySignal>());

        var registrationNames = registrations.GetAll()
            .Where(registration => linkedIds.Contains(registration.Id))
            .ToDictionary(registration => registration.Id, registration => registration.DisplayName);
        var websiteObservedAt = websiteEvidence.CompletedAtUtc;
        var signals = incidents.GetAll()
            .Where(incident => linkedIds.Contains(incident.RegistrationId))
            .Where(incident => incident.Status != IncidentStatus.Resolved)
            .Where(incident => AbsoluteDifference(incident.LastSeenUtc, websiteObservedAt) <= CorrelationWindow)
            .OrderByDescending(incident => incident.Severity)
            .ThenByDescending(incident => incident.LastSeenUtc)
            .Take(MaxSignals)
            .Select(incident => new WebsiteDependencySignal(
                incident.RegistrationId,
                registrationNames.GetValueOrDefault(incident.RegistrationId, incident.RegistrationId.ToString("D")),
                Bound(incident.RuleId, 80),
                incident.Severity,
                incident.LastSeenUtc,
                Bound(incident.Title, 180)))
            .ToArray();

        if (signals.Length == 0)
        {
            return new(true, "none",
                "Linked dependencies have no active incident inside the ±10 minute correlation window. The website probable layer is not corroborated by Monitor dependency incidents.",
                signals);
        }

        var websiteRule = websiteEvidence.RuleId;
        var hasDatabaseSignal = signals.Any(signal => signal.RuleId.StartsWith("database.", StringComparison.Ordinal) || signal.RuleId.StartsWith("blocking.", StringComparison.Ordinal));
        var hasInfrastructureSignal = signals.Any(signal =>
            signal.RuleId.StartsWith("memory.", StringComparison.Ordinal) ||
            signal.RuleId.StartsWith("performance.", StringComparison.Ordinal) ||
            signal.RuleId.StartsWith("storage.", StringComparison.Ordinal) ||
            signal.RuleId.StartsWith("snapshot.", StringComparison.Ordinal));

        if (websiteRule == "http.5xx" && hasDatabaseSignal)
        {
            return new(true, "high",
                "HTTP 5xx is contemporaneous with an active linked database/SQL incident. A backend dependency is a corroborated plausible contributor, but this does not prove database root cause.",
                signals);
        }

        if ((websiteRule is "http.5xx" or "network.timeout" or "performance.slow") && hasInfrastructureSignal)
        {
            return new(true, "medium",
                "The website failure/degradation overlaps an active linked infrastructure/performance incident. Shared dependency impact is plausible but not proven.",
                signals);
        }

        if (websiteRule is "dns.failure" or "network.connect-failure")
        {
            return new(true, "medium",
                "The website has direct DNS/TCP failure evidence and linked dependencies also report incidents in the same window. This supports a broader path/host impact hypothesis without proving a single network root cause.",
                signals);
        }

        return new(true, "low",
            "Linked dependency incidents overlap the website evidence, but their rule families do not strongly corroborate the website probable layer. Treat them as context only.",
            signals);
    }

    private static TimeSpan AbsoluteDifference(DateTimeOffset left, DateTimeOffset right)
    {
        var difference = left - right;
        return difference < TimeSpan.Zero ? -difference : difference;
    }

    private static string Bound(string value, int max) => value.Length <= max ? value : value[..max];
}
