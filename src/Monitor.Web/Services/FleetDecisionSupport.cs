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

public sealed record FleetRoutingSummary(
    int EvaluatedIncidents,
    int Page,
    int Notify,
    int Queue,
    int None,
    int Suppressed,
    int InMaintenance,
    int Unassigned);

public sealed record FleetCorrelationSummary(
    int EvaluatedIncidents,
    int TotalClusters,
    int CriticalClusters,
    int WarningClusters,
    int InfoClusters,
    int MultiServerClusters,
    int MaxAffectedServers,
    double HighestScore);

public sealed record FleetDecisionSupportSnapshot(
    TimeSpan CorrelationWindow,
    int InputIncidents,
    IReadOnlyList<SignalCluster> Correlations,
    IReadOnlyList<FleetRoutingSuggestion> RoutingSuggestions,
    FleetRoutingSummary? RoutingSummary = null,
    FleetCorrelationSummary? CorrelationSummary = null);

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
            item.AtUtc)).ToArray();

        FleetCorrelationSummary? correlationSummary = null;
        IReadOnlyList<SignalCluster> correlations;
        if (incidents.Length <= Batch400FleetCorrelation.MaxClusterLimit)
        {
            var allCorrelations = Batch400FleetCorrelation.Correlate(signals, window, Batch400FleetCorrelation.MaxClusterLimit);
            correlationSummary = new FleetCorrelationSummary(
                incidents.Length,
                allCorrelations.Count,
                allCorrelations.Count(item => item.Severity == B400Severity.Critical),
                allCorrelations.Count(item => item.Severity == B400Severity.Warning),
                allCorrelations.Count(item => item.Severity == B400Severity.Info),
                allCorrelations.Count(item => item.AffectedServers > 1),
                allCorrelations.Count == 0 ? 0 : allCorrelations.Max(item => item.AffectedServers),
                allCorrelations.Count == 0 ? 0 : allCorrelations.Max(item => item.Score));
            correlations = allCorrelations.Take(MaxItems).ToArray();
        }
        else
        {
            correlations = Batch400FleetCorrelation.Correlate(signals, window, MaxItems);
        }

        var routingDecisions = incidents.Select(item =>
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
            return new FleetRoutingDecision(item, Batch300AlertRouting.Decide(input));
        }).ToArray();

        var routingSummary = new FleetRoutingSummary(
            routingDecisions.Length,
            routingDecisions.Count(item => item.Decision.Route == AlertRoute.Page),
            routingDecisions.Count(item => item.Decision.Route == AlertRoute.Notify),
            routingDecisions.Count(item => item.Decision.Route == AlertRoute.Queue),
            routingDecisions.Count(item => item.Decision.Route == AlertRoute.None),
            routingDecisions.Count(item => item.Incident.Suppressed),
            routingDecisions.Count(item => item.Incident.InMaintenance),
            routingDecisions.Count(item => string.Equals(item.Decision.Owner, "unassigned", StringComparison.Ordinal)));

        var routing = routingDecisions.Take(MaxItems).Select(item => new FleetRoutingSuggestion(
            item.Incident.IncidentId,
            item.Incident.RegistrationId.ToString("D"),
            item.Incident.RuleId,
            Batch300AlertRouting.NormalizeEnvironment(item.Incident.Environment),
            item.Incident.Severity,
            item.Decision.Route,
            item.Decision.EscalationTier,
            item.Decision.Cooldown,
            item.Decision.Owner,
            item.Decision.Reason,
            item.Decision.DedupKey,
            item.Incident.Suppressed,
            item.Incident.InMaintenance)).ToArray();

        return new(window, incidents.Length, correlations, routing, routingSummary, correlationSummary);
    }

    private static B400Severity ToB400Severity(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Critical => B400Severity.Critical,
        FindingSeverity.Warning => B400Severity.Warning,
        _ => B400Severity.None
    };

    private sealed record FleetRoutingDecision(
        FleetDecisionIncident Incident,
        AlertRoutingDecision Decision);
}
