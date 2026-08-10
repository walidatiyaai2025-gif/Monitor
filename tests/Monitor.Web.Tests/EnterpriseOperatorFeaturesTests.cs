using System.IO.Compression;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class EnterpriseOperatorFeaturesTests
{
    [Fact]
    public void B100_091_MaintenanceWindow_IsBoundedAndHasExplicitActiveSemantics()
    {
        var now = new DateTimeOffset(2026, 8, 11, 2, 0, 0, TimeSpan.Zero);
        var active = EnterpriseOperatorValidation.BuildWindow(now.AddMinutes(-5), now.AddMinutes(25), "Approved patching");
        Assert.NotNull(active);
        Assert.True(EnterpriseOperatorPolicy.IsWindowActive(active, now));
        Assert.False(EnterpriseOperatorPolicy.IsWindowActive(active, now.AddHours(1)));
        Assert.Throws<ArgumentException>(() => EnterpriseOperatorValidation.BuildWindow(now, now, "bad"));
        Assert.Throws<ArgumentException>(() => EnterpriseOperatorValidation.BuildWindow(now, now.AddDays(32), "too long"));
    }

    [Fact]
    public void B100_092_ServerTagsAndGroups_AreNormalizedDeduplicatedAndBounded()
    {
        var now = DateTimeOffset.UtcNow;
        var metadata = new ServerOperatorMetadata(Guid.NewGuid(), ServerEnvironmentClass.Production, " Core DBA ", ["finance", "FINANCE", "tier-1"], null, null, now);
        var normalized = EnterpriseOperatorValidation.NormalizeServer(metadata, now);

        Assert.Equal("Core DBA", normalized.Group);
        Assert.Equal(2, normalized.Tags.Length);
        Assert.Contains("finance", normalized.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("tier-1", normalized.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Throws<ArgumentException>(() => EnterpriseOperatorValidation.ParseTags("safe,bad tag"));
    }

    [Fact]
    public void B100_093_EnvironmentClassification_PersistsAcrossFileStoreRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), $"monitor-enterprise-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "operator-metadata.json");
            var time = new MutableTimeProvider(DateTimeOffset.UtcNow);
            var id = Guid.NewGuid();
            var first = new FileOperatorMetadataStore(path, time);
            first.UpsertServer(new ServerOperatorMetadata(id, ServerEnvironmentClass.Staging, "Payments", ["tier-2"], null, null, time.GetUtcNow()));

            var restarted = new FileOperatorMetadataStore(path, time);
            var loaded = restarted.GetServer(id);
            Assert.Equal(ServerEnvironmentClass.Staging, loaded.Environment);
            Assert.Equal("Payments", loaded.Group);
            Assert.Equal(["tier-2"], loaded.Tags);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void B100_094_AlertSuppressionWindow_IsPersistedAndEvaluatedWithoutMutatingIncidents()
    {
        var now = new DateTimeOffset(2026, 8, 11, 2, 0, 0, TimeSpan.Zero);
        var store = new InMemoryOperatorMetadataStore(new MutableTimeProvider(now));
        var id = Guid.NewGuid();
        var suppression = EnterpriseOperatorValidation.BuildWindow(now.AddMinutes(-1), now.AddMinutes(59), "Change freeze");
        store.UpsertServer(new ServerOperatorMetadata(id, ServerEnvironmentClass.Production, null, [], null, suppression, now));

        var loaded = store.GetServer(id);
        Assert.True(EnterpriseOperatorPolicy.IsAlertSuppressed(loaded, now));
        Assert.False(EnterpriseOperatorPolicy.IsAlertSuppressed(loaded, now.AddHours(2)));
    }

    [Fact]
    public void B100_095_IncidentOwnership_IsDurableMetadataAndCanBeCleared()
    {
        var store = new InMemoryOperatorMetadataStore(TimeProvider.System);
        const string incidentId = "incident:owner:test";
        store.AssignIncident(incidentId, "DBA-OnCall");
        Assert.Equal("DBA-OnCall", store.GetIncident(incidentId).Assignee);

        store.AssignIncident(incidentId, null);
        Assert.Null(store.GetIncident(incidentId).Assignee);
        Assert.Throws<ArgumentException>(() => store.AssignIncident(incidentId, new string('A', EnterpriseOperatorValidation.MaxAssigneeLength + 1)));
    }

    [Fact]
    public void B100_096_IncidentNotes_AreBoundedAndRejectSecretBearingText()
    {
        var store = new InMemoryOperatorMetadataStore(TimeProvider.System);
        const string incidentId = "incident:notes:test";
        for (var index = 0; index < EnterpriseOperatorValidation.MaxNotesPerIncident + 5; index++)
            store.AddIncidentNote(incidentId, "operator", $"note-{index}");

        var notes = store.GetIncident(incidentId).Notes;
        Assert.Equal(EnterpriseOperatorValidation.MaxNotesPerIncident, notes.Length);
        Assert.DoesNotContain(notes, item => item.Text == "note-0");
        Assert.Contains(notes, item => item.Text == $"note-{EnterpriseOperatorValidation.MaxNotesPerIncident + 4}");
        Assert.Throws<ArgumentException>(() => store.AddIncidentNote(incidentId, "operator", "Password=super-secret-canary-20260811"));
    }

    [Fact]
    public void B100_097_RecommendationAcknowledgment_IsStableBoundedState()
    {
        var store = new InMemoryOperatorMetadataStore(TimeProvider.System);
        const string incidentId = "incident:recommendation:test";
        const string key = "rec:v1:0123456789ABCDEF01234567";

        store.SetRecommendationAcknowledged(incidentId, key, true);
        Assert.Contains(key, store.GetIncident(incidentId).AcknowledgedRecommendationKeys);
        store.SetRecommendationAcknowledged(incidentId, key, false);
        Assert.DoesNotContain(key, store.GetIncident(incidentId).AcknowledgedRecommendationKeys);
        Assert.Throws<ArgumentException>(() => store.SetRecommendationAcknowledged(incidentId, "unsafe/key", true));
    }

    [Fact]
    public void B100_098_CsvExport_IsFormulaSafeAndUsesCachePeekOnly()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var registration = Registration("=2+3");
        registrations.Upsert(registration);
        var cache = new PeekOnlyCache();
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        metadata.UpsertServer(new ServerOperatorMetadata(registration.Id, ServerEnvironmentClass.Production, "+Finance", ["tier-1"], null, null, DateTimeOffset.UtcNow));
        var service = new SafeCsvReportService(registrations, cache, metadata);

        var csv = Encoding.UTF8.GetString(service.BuildServerReport());

        Assert.Contains("\"'=2+3\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"'+Finance\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("sql.example.internal", csv, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, cache.PeekCalls);
        Assert.Equal(0, cache.CollectionCalls);
    }

    [Fact]
    public async Task B100_099_DiagnosticsPackage_IsBoundedAndContainsOnlyAggregateRedactedState()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        registrations.Upsert(Registration("super-secret-canary-20260811"));
        var incidents = new InMemoryHealthIncidentRepository();
        var now = DateTimeOffset.UtcNow;
        incidents.Apply([new HealthFinding(Guid.NewGuid(), "database.unavailable", FindingSeverity.Warning, "Canary incident", "Password=should-never-be-exported", now)]);
        var operatorMetadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        operatorMetadata.AddIncidentNote("incident:redaction:test", "operator", "safe note content");
        var service = new RedactedDiagnosticsPackageService(
            new FakeReadinessService(), registrations, incidents, operatorMetadata,
            new DeploymentTopologyOptions { Mode = DeploymentTopology.SingleNode }, TimeProvider.System);

        var package = await service.BuildAsync();
        Assert.InRange(package.Length, 1, 256 * 1024);

        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        var allText = string.Join('\n', archive.Entries.Select(entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }));
        Assert.Contains("formatVersion", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-canary-20260811", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("safe note content", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("sql.example.internal", allText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void B100_100_ReleaseCandidateEnterpriseRoutes_AreAuthorizedAntiforgeryProtectedAndZeroSqlOnReports()
    {
        var methods = typeof(EnterpriseOperationsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var mutating = methods.Where(method => method.GetCustomAttributes(inherit: true).Any(attribute => attribute is HttpPostAttribute)).ToArray();
        Assert.NotEmpty(mutating);
        Assert.All(mutating, method =>
        {
            Assert.Contains(method.GetCustomAttributes(inherit: true), attribute => attribute is ValidateAntiForgeryTokenAttribute);
            Assert.Contains(method.GetCustomAttributes(inherit: true), attribute => attribute is AuthorizeAttribute authorize && !string.IsNullOrWhiteSpace(authorize.Policy));
        });

        var csvMethod = methods.Single(method => method.Name == nameof(EnterpriseOperationsController.ServerCsv));
        Assert.Contains(csvMethod.GetCustomAttributes(inherit: true), attribute => attribute is HttpGetAttribute);
        var diagnosticsMethod = methods.Single(method => method.Name == nameof(EnterpriseOperationsController.Diagnostics));
        Assert.Contains(diagnosticsMethod.GetCustomAttributes(inherit: true), attribute => attribute is AuthorizeAttribute authorize && authorize.Policy == MonitorPolicies.Manage);
    }

    private static ServerRegistration Registration(string displayName) => new(
        Guid.NewGuid(),
        displayName,
        new SqlServerEndpoint("sql.example.internal", 1433),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        DateTimeOffset.UtcNow);

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class PeekOnlyCache : IServerHealthSnapshotCache
    {
        public int PeekCalls { get; private set; }
        public int CollectionCalls { get; private set; }
        public SnapshotCacheResult? Peek(Guid registrationId)
        {
            PeekCalls++;
            return null;
        }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CollectionCalls++;
            throw new InvalidOperationException("CSV GET must not collect monitored SQL.");
        }
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CollectionCalls++;
            throw new InvalidOperationException("CSV GET must not collect monitored SQL.");
        }
    }

    private sealed class FakeReadinessService : IApplicationReadinessService
    {
        public Task<ApplicationReadinessSnapshot> CheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ApplicationReadinessSnapshot(
            ApplicationReadinessStatus.Ready,
            "Ready.",
            SharedStateReadinessStatus.Disabled,
            true,
            true,
            true,
            DateTimeOffset.UtcNow,
            SharedStateSchemaVersion: null,
            SharedStorageReady: false));
    }
}
