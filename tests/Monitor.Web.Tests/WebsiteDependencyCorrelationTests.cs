using Monitor.Web.Models;
using Monitor.Web.Services;

namespace Monitor.Web.Tests;

public sealed class WebsiteDependencyCorrelationTests
{
    [Fact]
    public void Unlinked_target_reports_no_dependency_correlation()
    {
        var service = Service(out _, out _);
        var target = Target([]);

        var result = service.Assess(target, WebsiteEvidence(target.Id, "http.5xx"));

        Assert.False(result.HasConfiguredDependencies);
        Assert.Empty(result.Signals);
        Assert.Equal("none", result.Confidence);
    }

    [Fact]
    public void Linked_database_incident_corroborates_http_5xx_without_claiming_root_cause()
    {
        var service = Service(out var registrations, out var incidents);
        var serverId = Guid.NewGuid();
        registrations.Upsert(Registration(serverId, "SQL-PROD-01"));
        var observed = DateTimeOffset.Parse("2026-08-19T08:00:00Z");
        incidents.Apply([
            new HealthFinding(serverId, "database.unavailable", FindingSeverity.Critical,
                "Databases unavailable", "1 database is offline.", observed.AddMinutes(-2))
        ]);
        var target = Target([serverId]);

        var result = service.Assess(target, WebsiteEvidence(target.Id, "http.5xx", observed));

        Assert.True(result.HasConfiguredDependencies);
        Assert.Equal("high", result.Confidence);
        var signal = Assert.Single(result.Signals);
        Assert.Equal("SQL-PROD-01", signal.RegistrationName);
        Assert.Equal("database.unavailable", signal.RuleId);
        Assert.Contains("plausible contributor", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not prove", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stale_dependency_incident_outside_window_is_not_used_as_corroboration()
    {
        var service = Service(out var registrations, out var incidents);
        var serverId = Guid.NewGuid();
        registrations.Upsert(Registration(serverId, "SQL-PROD-01"));
        var observed = DateTimeOffset.Parse("2026-08-19T08:00:00Z");
        incidents.Apply([
            new HealthFinding(serverId, "database.unavailable", FindingSeverity.Critical,
                "Databases unavailable", "1 database is offline.", observed.AddMinutes(-30))
        ]);
        var target = Target([serverId]);

        var result = service.Assess(target, WebsiteEvidence(target.Id, "http.5xx", observed));

        Assert.Equal("none", result.Confidence);
        Assert.Empty(result.Signals);
        Assert.Contains("no active incident inside", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unrelated_dependency_rule_is_context_only()
    {
        var service = Service(out var registrations, out var incidents);
        var serverId = Guid.NewGuid();
        registrations.Upsert(Registration(serverId, "SQL-PROD-01"));
        var observed = DateTimeOffset.Parse("2026-08-19T08:00:00Z");
        incidents.Apply([
            new HealthFinding(serverId, "backup.full-gap", FindingSeverity.Warning,
                "Full backup gap", "Backup evidence.", observed)
        ]);
        var target = Target([serverId]);

        var result = service.Assess(target, WebsiteEvidence(target.Id, "http.5xx", observed));

        Assert.Equal("low", result.Confidence);
        Assert.Single(result.Signals);
        Assert.Contains("context only", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Network_failure_plus_concurrent_dependency_incidents_supports_broad_impact_hypothesis_only()
    {
        var service = Service(out var registrations, out var incidents);
        var serverId = Guid.NewGuid();
        registrations.Upsert(Registration(serverId, "SQL-PROD-01"));
        var observed = DateTimeOffset.Parse("2026-08-19T08:00:00Z");
        incidents.Apply([
            new HealthFinding(serverId, "snapshot.stale", FindingSeverity.Warning,
                "Snapshot is stale", "Collection evidence is stale.", observed)
        ]);
        var target = Target([serverId]);

        var result = service.Assess(target, WebsiteEvidence(target.Id, "network.connect-failure", observed));

        Assert.Equal("medium", result.Confidence);
        Assert.Contains("broader path/host impact hypothesis", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without proving", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Target_validation_bounds_and_deduplicates_linked_dependency_contract()
    {
        var duplicate = Guid.NewGuid();
        var target = Target([duplicate, duplicate]);

        var result = WebsiteTargetValidator.Validate(target);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }

    private static WebsiteDependencyCorrelationService Service(
        out InMemoryServerRegistrationRepository registrations,
        out InMemoryHealthIncidentRepository incidents)
    {
        registrations = new InMemoryServerRegistrationRepository();
        incidents = new InMemoryHealthIncidentRepository();
        return new WebsiteDependencyCorrelationService(registrations, incidents);
    }

    private static WebsiteTargetDefinition Target(IReadOnlyList<Guid> linked) => new(
        Guid.NewGuid(),
        "Citizen Portal",
        "https://example.com/health",
        "production",
        LinkedRegistrationIds: linked);

    private static WebsiteProbeHistoryPoint WebsiteEvidence(Guid targetId, string ruleId, DateTimeOffset? at = null) => new(
        targetId,
        at ?? DateTimeOffset.Parse("2026-08-19T08:00:00Z"),
        ruleId == "performance.slow" ? WebsiteProbeState.Degraded : WebsiteProbeState.Down,
        ruleId,
        ruleId.StartsWith("network.", StringComparison.Ordinal) ? "Network / listener path" : "Web server / proxy / application",
        "high",
        ruleId.StartsWith("http.", StringComparison.Ordinal) ? 500 : null,
        120,
        DateTimeOffset.Parse("2027-08-19T00:00:00Z"),
        "example.com",
        0,
        $"Observed {ruleId} evidence.");

    private static ServerRegistration Registration(Guid id, string name) => new(
        id,
        name,
        new SqlServerEndpoint("sql-prod-01"),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        DateTimeOffset.Parse("2026-08-01T00:00:00Z"));
}
