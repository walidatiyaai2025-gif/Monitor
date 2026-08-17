using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record FleetDecisionIncident(
    string IncidentId,
    Guid RegistrationId,
    string RuleId,
    FindingSeverity Severity,
    DateTimeOffset AtUtc,
    string Environment,
    bool Suppressed,
    bool InMaintenance,
    string? Assignee);

public sealed record FleetRoutingSuggestion(
    string IncidentId,
    string ServerKey,
    string RuleId,
    string Environment,
    FindingSeverity Severity,
    AlertRoute SuggestedRoute,
    int EscalationTier,
    TimeSpan Cooldown,
    string Owner,
    string Reason,
    string DedupKey,
    bool Suppressed,
    bool InMaintenance);

public sealed record FleetDecisionSupportSnapshot(
    TimeSpan CorrelationWindow,
    int InputIncidents,
    IReadOnlyList<SignalCluster> Correlations,
    IReadOnlyList<FleetRoutingSuggestion> RoutingSuggestions);

public static class FleetDecisionSupport
{
    public const int MaxItems = 20;

    public static FleetDecisionSupportSnapshot Build(IEnumerable<FleetDecisionIncident> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var incidents = source
            .Where(item => !string.IsNullOrWhiteSpace(item.IncidentId) && item.RegistrationId != Guid.Empty && !string.IsNullOrWhiteSpace(item.RuleId))
            .OrderByDescending(item => item.Severity)
            .ThenByDescending(item => item.AtUtc)
            .ThenBy(item => item.IncidentId, StringComparer.Ordinal)
            .ToArray();
        var window = Batch400FleetCorrelation.ClampWindow(TimeSpan.Zero);

        var signals = incidents.Select(item => new FleetSignal(
            item.RegistrationId.ToString("D"),
            item.Environment,
            item.RuleId,
            ToB400Severity(item.Severity),
            item.AtUtc));
        var correlations = Batch400FleetCorrelation.Correlate(signals, window, MaxItems);

        var routing = incidents.Take(MaxItems).Select(item =>
        {
            var severity = ToB400Severity(item.Severity);
            var input = new AlertRoutingInput(
                item.RuleId,
                item.Environment,
                Batch400FleetCorrelation.SeverityWeight(severity),
                item.Suppressed,
                item.InMaintenance,
                item.Assignee,
                item.AtUtc);
            var decision = Batch300AlertRouting.Decide(input);
            return new FleetRoutingSuggestion(
                item.IncidentId,
                item.RegistrationId.ToString("D"),
                item.RuleId,
                Batch300AlertRouting.NormalizeEnvironment(item.Environment),
                item.Severity,
                decision.Route,
                decision.EscalationTier,
                decision.Cooldown,
                decision.Owner,
                decision.Reason,
                decision.DedupKey,
                item.Suppressed,
                item.InMaintenance);
        }).ToArray();

        return new(window, incidents.Length, correlations, routing);
    }

    private static B400Severity ToB400Severity(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Critical => B400Severity.Critical,
        FindingSeverity.Warning => B400Severity.Warning,
        _ => B400Severity.None
    };
}
