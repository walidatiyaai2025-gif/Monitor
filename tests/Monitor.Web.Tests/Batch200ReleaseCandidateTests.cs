using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch200ReleaseCandidateTests
{
    [Fact]
    public void B200_091_HelpSurfaceExplainsCoreOperatorWorkflowsAndLinks()
    {
        var help = Read("src/Monitor.Web/Views/EnterpriseHelp/Help.cshtml");
        var controller = typeof(EnterpriseHelpController);
        var authorization = Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(MonitorPolicies.Read, authorization.Policy);
        Assert.Contains("Enterprise Operations Help", help, StringComparison.Ordinal);
        Assert.Contains("Fleet Intelligence", help, StringComparison.Ordinal);
        Assert.Contains("Persistence Readiness", help, StringComparison.Ordinal);
        Assert.Contains("Scheduled collection policy", help, StringComparison.Ordinal);
        Assert.Contains("Incident actionability", help, StringComparison.Ordinal);
        Assert.Contains("approved DBA tool", help, StringComparison.Ordinal);
    }

    [Fact]
    public void B200_092_EnterpriseCssHasResponsiveAndReducedMotionContracts()
    {
        var css = Read("src/Monitor.Web/wwwroot/css/enterprise-operations.css");

        Assert.Contains("@media(max-width:980px)", css, StringComparison.Ordinal);
        Assert.Contains("@media(max-width:640px)", css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion:reduce", css, StringComparison.Ordinal);
        Assert.Contains("overflow-x:auto", css, StringComparison.Ordinal);
        Assert.Contains("width:100%", css, StringComparison.Ordinal);
    }

    [Fact]
    public void B200_093_DegradedReadinessIsOpaqueAndDoesNotThrow()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var service = new EnterprisePersistenceReadinessService(new CorruptOperatorMetadataStore(), clock);

        var readiness = service.Read();

        Assert.Equal("degraded", readiness.Status);
        Assert.Equal(0, readiness.ServerMetadataRecords);
        Assert.Equal(0, readiness.IncidentMetadataRecords);
        Assert.Equal(clock.GetUtcNow(), readiness.CheckedAtUtc);
        Assert.DoesNotContain("connection", readiness.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", readiness.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unavailable or invalid", readiness.Message, StringComparison.OrdinalIgnoreCase);

        var view = Read("src/Monitor.Web/Views/EnterpriseHelp/Readiness.cshtml");
        Assert.Contains("Degraded enterprise state", view, StringComparison.Ordinal);
        Assert.Contains("never connects to a monitored SQL target", view, StringComparison.Ordinal);
    }

    [Fact]
    public void B200_094_ReadyPersistenceProjectionReportsBoundedControlPlaneCounts()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var metadata = new InMemoryOperatorMetadataStore(clock);
        var serverId = Guid.NewGuid();
        metadata.UpsertServer(new ServerOperatorMetadata(serverId, ServerEnvironmentClass.Production, "core", ["tier-1"], null, null, clock.GetUtcNow()));
        metadata.AssignIncident("incident-ready", "DBA-OnCall");
        var service = new EnterprisePersistenceReadinessService(metadata, clock);

        var readiness = service.Read();

        Assert.Equal("ready", readiness.Status);
        Assert.Equal(1, readiness.ServerMetadataRecords);
        Assert.Equal(1, readiness.IncidentMetadataRecords);
        Assert.Contains("Monitor-owned control-plane state only", readiness.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void B200_095_MaintenanceSuppressionRunbookDocumentsFailClosedAndOverrideSemantics()
    {
        var runbook = Read("docs/MAINTENANCE_SUPPRESSION_RUNBOOK.md");

        Assert.Contains("start-inclusive and end-exclusive", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scheduled collection fails closed", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("manual refresh", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicit override", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never resolves an incident", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("credentials", runbook, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void B200_096_IncidentCollaborationRunbookKeepsOperatorContextSeparateFromEvidence()
    {
        var runbook = Read("docs/INCIDENT_COLLABORATION_RUNBOOK.md");

        Assert.Contains("replay key", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hashed audit receipt", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("separate from `HealthIncident.Evidence`", runbook, StringComparison.Ordinal);
        Assert.Contains("not execution", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("autonomously run remediation SQL", runbook, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void B200_097_UpgradeContractPreservesBatch100RoutesAndAvoidsNewSqlPermissions()
    {
        var upgrade = Read("docs/BATCH_200_UPGRADE.md");
        var enterprise = Read("src/Monitor.Web/Controllers/EnterpriseOperationsController.cs");
        var health = Read("src/Monitor.Web/Controllers/HealthController.cs");
        var operations = Read("src/Monitor.Web/Controllers/OperationsController.cs");

        Assert.Contains("/reports/servers.csv", upgrade, StringComparison.Ordinal);
        Assert.Contains("/diagnostics/package", upgrade, StringComparison.Ordinal);
        Assert.Contains("/health/live", upgrade, StringComparison.Ordinal);
        Assert.Contains("/health/ready", upgrade, StringComparison.Ordinal);
        Assert.Contains("does not require a new monitored-SQL permission or query", upgrade, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("/reports/servers.csv", enterprise, StringComparison.Ordinal);
        Assert.Contains("/diagnostics/package", enterprise, StringComparison.Ordinal);
        Assert.Contains("/health/live", health, StringComparison.Ordinal);
        Assert.Contains("/health/ready", health, StringComparison.Ordinal);
        Assert.Contains("/servers/{id:guid}/refresh", operations, StringComparison.Ordinal);
    }

    [Fact]
    public void B200_098_DeploymentSmokeUsesControlPlaneGetProbesAndNeverRefreshesSnapshots()
    {
        var smoke = Read("scripts/Smoke-Batch200.ps1");

        Assert.Contains("/health/live", smoke, StringComparison.Ordinal);
        Assert.Contains("/health/ready", smoke, StringComparison.Ordinal);
        Assert.Contains("/enterprise/readiness", smoke, StringComparison.Ordinal);
        Assert.Contains("/enterprise/help", smoke, StringComparison.Ordinal);
        Assert.Contains("/enterprise/fleet", smoke, StringComparison.Ordinal);
        Assert.Contains("/reports/servers-v2.csv", smoke, StringComparison.Ordinal);
        Assert.Contains("-Method Get", smoke, StringComparison.Ordinal);
        Assert.DoesNotContain("/refresh", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RefreshServer", smoke, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void B200_099_ReleaseCandidateLedgerContainsExactlyOneHundredTaskIdentifiersAndVerifiedFirstNinety()
    {
        var ledger = Read("docs/BATCH_200.md");
        var ids = System.Text.RegularExpressions.Regex.Matches(ledger, @"\| (B200-\d{3}) \|")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(100, ids.Length);
        Assert.Equal(100, ids.Distinct(StringComparer.Ordinal).Count());
        for (var number = 1; number <= 100; number++)
            Assert.Contains($"B200-{number:000}", ids, StringComparer.Ordinal);
        for (var number = 1; number <= 90; number++)
            Assert.Contains($"| B200-{number:000} |", ledger, StringComparison.Ordinal);
        Assert.Contains("CI VERIFIED", ledger, StringComparison.Ordinal);
        Assert.Contains("Monitoring, navigation, reporting and diagnostics GETs never initiate collection", ledger, StringComparison.Ordinal);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CorruptOperatorMetadataStore : IOperatorMetadataStore
    {
        public ServerOperatorMetadata GetServer(Guid registrationId) => throw new InvalidDataException("corrupt");
        public void UpsertServer(ServerOperatorMetadata metadata) => throw new NotSupportedException();
        public IncidentOperatorMetadata GetIncident(string incidentId) => throw new InvalidDataException("corrupt");
        public void AssignIncident(string incidentId, string? assignee) => throw new NotSupportedException();
        public void AddIncidentNote(string incidentId, string actor, string note) => throw new NotSupportedException();
        public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged) => throw new NotSupportedException();
        public EnterpriseOperatorSnapshot Snapshot() => throw new InvalidDataException("corrupt operator metadata");
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
