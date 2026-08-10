using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class HealthIncidentServiceTests
{
    private static readonly Guid RegistrationId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Observed = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluator_ProducesAllowlistedDeterministicFindings()
    {
        var snapshot = Snapshot() with
        {
            DatabaseOnline = 7,
            Databases = new(0, 0, 0, 1, 0, 0),
            Backups = new(5, 3, Observed.AddHours(-2)),
            Jobs = new(10, 9, 1),
            Blocking = new(2, 45_000),
            Memory = new(100, 10, 50, 90, true, false, "Low"),
            Performance = new(20, 12, 1)
        };

        var findings = new HealthRuleEvaluator().Evaluate(RegistrationId, snapshot, SnapshotFreshness.Stale);

        Assert.Contains(findings, item => item.RuleId == "snapshot.stale");
        Assert.Contains(findings, item => item.RuleId == "database.suspect" && item.Severity == FindingSeverity.Critical);
        Assert.Contains(findings, item => item.RuleId == "blocking.active" && item.Severity == FindingSeverity.Critical);
        Assert.Contains(findings, item => item.RuleId == "performance.runnable");
        Assert.DoesNotContain(findings, item => item.Evidence.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Repository_DeduplicatesAndIgnoresOlderEvidence()
    {
        var repository = new InMemoryHealthIncidentRepository();
        var finding = new HealthFinding(RegistrationId, "backup.full-gap", FindingSeverity.Warning, "Backup gap", "3 databases", Observed);

        repository.Apply([finding, finding with { ObservedAtUtc = Observed.AddMinutes(1) }]);
        repository.Apply([finding with { ObservedAtUtc = Observed.AddMinutes(-1) }]);

        var incident = Assert.Single(repository.GetAll());
        Assert.Equal(2, incident.Occurrences);
        Assert.Equal(Observed.AddMinutes(1), incident.LastSeenUtc);
        Assert.Equal(IncidentStatus.Open, incident.Status);
    }

    [Fact]
    public void MissingModules_DoNotProduceFalseHealthyOrCriticalFindings()
    {
        var findings = new HealthRuleEvaluator().Evaluate(RegistrationId, Snapshot(), SnapshotFreshness.Fresh);
        Assert.Empty(findings);
    }

    [Fact]
    public void FreshHealthyReconciliation_ResolvesButStaleDoesNot()
    {
        var repository = new InMemoryHealthIncidentRepository();
        var finding = new HealthFinding(RegistrationId, "agent.failed-job", FindingSeverity.Warning, "Failed job", "1 job", Observed);
        repository.Apply([finding]);

        repository.Reconcile(RegistrationId, Observed.AddMinutes(1), [], canResolve: false);
        Assert.Equal(IncidentStatus.Open, Assert.Single(repository.GetAll()).Status);

        repository.Reconcile(RegistrationId, Observed.AddMinutes(2), [], canResolve: true);
        Assert.Equal(IncidentStatus.Resolved, Assert.Single(repository.GetAll()).Status);
    }

    private static ServerHealthSnapshot Snapshot() => new(
        RegistrationId, "SQL01", "17", "Enterprise", null, 100, 10, 10, Observed);
}
