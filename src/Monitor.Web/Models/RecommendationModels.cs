namespace Monitor.Web.Models;

public enum RecommendationConfidence
{
    Medium,
    High
}

public sealed record RemediationStep(
    int Order,
    string Title,
    string Detail,
    bool RequiresChangeApproval);

public sealed record DiagnosticSqlProposal(
    string Title,
    string Purpose,
    string Sql,
    string SafetyNote);

public sealed record HealthRecommendation(
    string RuleId,
    FindingSeverity Severity,
    string Problem,
    string Evidence,
    string Rationale,
    RecommendationConfidence Confidence,
    IReadOnlyList<RemediationStep> Steps,
    DiagnosticSqlProposal? DiagnosticSql);

public sealed record IncidentRecommendationViewModel(
    HealthIncident Incident,
    HealthRecommendation Recommendation);
