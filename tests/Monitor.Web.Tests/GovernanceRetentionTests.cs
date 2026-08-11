using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class GovernanceRetentionTests
{
    [Fact]
    public void B200_051_DetectsOrphanedServerMetadata()
    {
        var f = Fixture();
        var orphan = Guid.NewGuid();
        f.Metadata.UpsertServer(new(orphan, ServerEnvironmentClass.Production, "legacy", ["orphan"], null, null, f.Clock.UtcNow));

        var plan = f.Service().DryRun();

        Assert.Contains(plan.Candidates, item => item.Kind == "server" && item.Key == orphan.ToString("D"));
    }

    [Fact]
    public void B200_052_PruneReceiptHidesResolvedIncidentFromCollaborationProjection()
    {
        var f = Fixture();
        var incident = f.Incidents.AddResolved(f.Clock.UtcNow.AddDays(-40));
        f.Metadata.AssignIncident(incident.Id, "LegacyOwner");

        Assert.Equal(1, f.Service().Apply("admin"));
        var rows = new IncidentCollaborationService(f.Metadata, f.Audit, f.Clock).QueryByAssignee([incident], null);
        Assert.Empty(rows);
        Assert.True(f.Service().IsIncidentPruned(incident.Id));
    }

    [Fact]
    public void B200_053_PruneReceiptHidesExpiredOperatorNote()
    {
        var f = Fixture();
        f.Clock.UtcNow = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        f.Metadata.AddIncidentNote("incident-note-retention", "operator", "Old operator note");
        var note = Assert.Single(f.Metadata.GetIncident("incident-note-retention").Notes);
        f.Clock.UtcNow = DateTimeOffset.Parse("2026-08-11T00:00:00Z");

        _ = f.Service().Apply("admin");
        var visible = new IncidentCollaborationService(f.Metadata, f.Audit, f.Clock).ReadNotes("incident-note-retention", 0, 20);

        Assert.Empty(visible);
        Assert.True(f.Service().IsNotePruned(note.Id));
    }

    [Fact]
    public void B200_054_AuditVisibleRetentionIsConfigurableAndBounded()
    {
        var f = Fixture();
        for (var index = 0; index < 150; index++) f.Audit.Append("actor", "action", $"target-{index}", "ok");
        var options = new GovernanceRetentionOptions { AuditMaxVisibleEvents = 120 };
        var service = f.Service(options);

        Assert.Equal(100, service.ReadGovernedAudit(0, 500).Count);
        Assert.Throws<InvalidOperationException>(() => new GovernanceRetentionOptions { AuditMaxVisibleEvents = 99 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new GovernanceRetentionOptions { AuditMaxVisibleEvents = 1001 }.Validate());
    }

    [Fact]
    public void B200_055_BackupRetentionConfigurationRejectsUnsafeBounds()
    {
        Assert.Throws<InvalidOperationException>(() => new GovernanceRetentionOptions { BackupRetentionCount = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new GovernanceRetentionOptions { BackupRetentionCount = 51 }.Validate());
        new GovernanceRetentionOptions { BackupRetentionCount = 10 }.Validate();
    }

    [Fact]
    public void B200_056_HistoryRetentionConfigurationRejectsUnsafeBounds()
    {
        Assert.Throws<InvalidOperationException>(() => new GovernanceRetentionOptions { HistoryRetentionHours = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new GovernanceRetentionOptions { HistoryRetentionHours = 25 }.Validate());
        new GovernanceRetentionOptions { HistoryRetentionHours = 24 }.Validate();
    }

    [Fact]
    public void B200_057_DryRunDoesNotMutateAuditOrMetadata()
    {
        var f = Fixture();
        var orphan = Guid.NewGuid();
        f.Metadata.UpsertServer(new(orphan, ServerEnvironmentClass.Production, null, [], null, null, f.Clock.UtcNow));
        var before = f.Metadata.Snapshot();

        var plan = f.Service().DryRun();

        Assert.NotEmpty(plan.Candidates);
        Assert.Empty(f.Audit.Read(0, 100));
        Assert.Equal(before, f.Metadata.Snapshot());
    }

    [Fact]
    public void B200_058_CleanupApplyRequiresManagePostAndAntiforgery()
    {
        var controllerAuthorization = Assert.Single(typeof(GovernanceController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(MonitorPolicies.Manage, controllerAuthorization.Policy);
        var method = typeof(GovernanceController).GetMethod(nameof(GovernanceController.Apply), BindingFlags.Public | BindingFlags.Instance)!;
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void B200_059_ApplyCreatesPerCandidateAndSummaryAuditTrail()
    {
        var f = Fixture();
        var orphan = Guid.NewGuid();
        f.Metadata.UpsertServer(new(orphan, ServerEnvironmentClass.Production, null, [], null, null, f.Clock.UtcNow));

        var applied = f.Service().Apply("admin");

        Assert.Equal(1, applied);
        Assert.Contains(f.Audit.Read(0, 100), item => item.Action == "governance.prune.server" && item.Target == orphan.ToString("D") && item.Outcome == "applied");
        Assert.Contains(f.Audit.Read(0, 100), item => item.Action == "governance.cleanup" && item.Outcome == "applied:1");
    }

    [Fact]
    public void B200_060_GovernanceViewDeclaresDryRunAndNoSqlSemantics()
    {
        var repo = FindRepoRoot();
        var view = File.ReadAllText(Path.Combine(repo, "src", "Monitor.Web", "Views", "Governance", "Index.cshtml"));

        Assert.Contains("DRY RUN", view, StringComparison.Ordinal);
        Assert.Contains("never initiates monitored SQL collection", view, StringComparison.Ordinal);
        Assert.Contains("Apply audited pruning", view, StringComparison.Ordinal);
    }

    private static FixtureState Fixture()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        return new(clock, new RegistrationStore(), new IncidentStore(), new InMemoryOperatorMetadataStore(clock), new AuditStore(clock));
    }

    private sealed record FixtureState(MutableTimeProvider Clock, RegistrationStore Registrations, IncidentStore Incidents, InMemoryOperatorMetadataStore Metadata, AuditStore Audit)
    {
        public GovernanceRetentionService Service(GovernanceRetentionOptions? options = null) => new(Registrations, Incidents, Metadata, Audit, Clock, options);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class RegistrationStore : IServerRegistrationRepository
    {
        private readonly List<ServerRegistration> _items = [];
        public IReadOnlyList<ServerRegistration> GetAll() => _items.ToArray();
        public ServerRegistration? GetById(Guid id) => _items.FirstOrDefault(item => item.Id == id);
        public void Upsert(ServerRegistration registration) { _items.RemoveAll(item => item.Id == registration.Id); _items.Add(registration); }
        public bool Remove(Guid id) => _items.RemoveAll(item => item.Id == id) > 0;
    }

    private sealed class IncidentStore : IHealthIncidentRepository
    {
        private readonly List<HealthIncident> _items = [];
        public HealthIncident AddResolved(DateTimeOffset seen)
        {
            var registrationId = Guid.NewGuid();
            var incident = new HealthIncident($"{registrationId:N}:resolved", registrationId, "memory.pressure", FindingSeverity.Warning, "Resolved", "Cached evidence", seen, seen, 1, IncidentStatus.Resolved);
            _items.Add(incident);
            return incident;
        }
        public void Apply(IEnumerable<HealthFinding> findings) => throw new NotSupportedException();
        public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve) => throw new NotSupportedException();
        public IReadOnlyList<HealthIncident> GetAll() => _items.ToArray();
        public HealthIncident? GetById(string id) => _items.FirstOrDefault(item => item.Id == id);
        public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next) => false;
    }

    private sealed class AuditStore(TimeProvider clock) : IAuditStore
    {
        private readonly List<AuditEvent> _items = [];
        public void Append(string actor, string action, string target, string outcome) => _items.Add(new(Guid.NewGuid(), clock.GetUtcNow(), actor, action, target, outcome));
        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => _items.OrderByDescending(item => item.OccurredAtUtc).Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 100)).ToArray();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root containing Monitor.sln was not found.");
    }
}
