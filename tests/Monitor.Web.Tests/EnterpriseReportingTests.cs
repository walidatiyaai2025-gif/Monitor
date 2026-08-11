using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class EnterpriseReportingTests
{
    [Fact]
    public void B200_031_ServerCsvAppliesEnterpriseFilters()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var first = Registration("Payments SQL");
        var second = Registration("HR SQL");
        var registrations = new RegistrationStore([first, second]);
        var metadata = new InMemoryOperatorMetadataStore(clock);
        metadata.UpsertServer(new(first.Id, ServerEnvironmentClass.Production, "payments", ["tier-1"], null, null, clock.UtcNow));
        metadata.UpsertServer(new(second.Id, ServerEnvironmentClass.Test, "hr", ["tier-2"], null, null, clock.UtcNow));
        var service = Service(registrations, new PeekOnlyCache(), metadata, clock: clock);

        var csv = Decode(service.Servers(new(ServerEnvironmentClass.Production, "payments", "tier-1")));

        Assert.Contains("Payments SQL", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("HR SQL", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("sql.internal", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void B200_032_IncidentCsvIsFormulaSafeAndOmitsEvidence()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var incident = Incident(clock.UtcNow);
        var incidents = new IncidentStore([incident]);
        var metadata = new InMemoryOperatorMetadataStore(clock);
        metadata.AssignIncident(incident.Id, "=WEBSERVICE(\"https://invalid\")");
        var service = Service(new RegistrationStore([]), new PeekOnlyCache(), metadata, incidents, clock: clock);

        var csv = Decode(service.Incidents(new()));

        Assert.Contains("'=WEBSERVICE", csv, StringComparison.Ordinal);
        Assert.DoesNotContain(incident.Evidence, csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Evidence", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void B200_033_HistoryCsvIsBoundedToRequestedWindow()
    {
        var now = DateTimeOffset.Parse("2026-08-11T00:00:00Z");
        var clock = new FixedTimeProvider(now);
        var registrationId = Guid.NewGuid();
        var history = new HistoryStore([
            new(registrationId, now.AddMinutes(-30), 5, 5, 50, 0, 1, SnapshotFreshness.Fresh),
            new(registrationId, now.AddHours(-7), 4, 5, 80, 2, 12, SnapshotFreshness.Stale)
        ]);
        var service = Service(new RegistrationStore([]), new PeekOnlyCache(), new InMemoryOperatorMetadataStore(clock), history: history, clock: clock);

        var csv = Decode(service.History(registrationId, TimeSpan.FromHours(1)));

        Assert.Contains(now.AddMinutes(-30).ToString("O"), csv, StringComparison.Ordinal);
        Assert.DoesNotContain(now.AddHours(-7).ToString("O"), csv, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.History(registrationId, TimeSpan.FromHours(25)));
    }

    [Fact]
    public void B200_034_AuditCsvEndpointRequiresManagePolicy()
    {
        var method = typeof(EnterpriseReportsController).GetMethod(nameof(EnterpriseReportsController.Audit), BindingFlags.Public | BindingFlags.Instance)!;

        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(MonitorPolicies.Manage, authorization.Policy);
    }

    [Fact]
    public void B200_035_CsvSchemaIsVersionedAndDeterministic()
    {
        var first = Decode(EnterpriseReportContract.Csv(["A", "B"], [["1", "2"]]));
        var second = Decode(EnterpriseReportContract.Csv(["A", "B"], [["1", "2"]]));

        Assert.Equal(first, second);
        Assert.Contains($"#schema,{EnterpriseReportContract.SchemaVersion}\n", first, StringComparison.Ordinal);
        Assert.Contains("\"A\",\"B\"\n", first, StringComparison.Ordinal);
    }

    [Fact]
    public void B200_036_CsvEnforcesRowAndByteCaps()
    {
        var manyRows = Enumerable.Range(0, EnterpriseReportContract.MaxRows + 50)
            .Select(index => (IReadOnlyList<string?>)[index.ToString()]);
        var bounded = Decode(EnterpriseReportContract.Csv(["Value"], manyRows));
        var lines = bounded.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(EnterpriseReportContract.MaxRows + 2, lines.Length);

        var wideHeaders = Enumerable.Range(0, 32).Select(index => $"C{index}").ToArray();
        var largeCell = new string('x', EnterpriseReportContract.MaxCellLength);
        var largeRows = Enumerable.Range(0, EnterpriseReportContract.MaxRows)
            .Select(_ => (IReadOnlyList<string?>)Enumerable.Repeat<string?>(largeCell, 32).ToArray());
        Assert.Throws<InvalidOperationException>(() => EnterpriseReportContract.Csv(wideHeaders, largeRows));
    }

    [Fact]
    public void B200_037_CsvUsesUtf8BomAndLfLineEndings()
    {
        var bytes = EnterpriseReportContract.Csv(["Name"], [["café"]]);

        Assert.True(bytes.Length > 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
        var text = Decode(bytes);
        Assert.Contains("café", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\n", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+SUM(A1:A2)")]
    [InlineData("-10+20")]
    [InlineData("@SUM(A1:A2)")]
    [InlineData("\t=cmd")]
    [InlineData("\r=cmd")]
    public void B200_038_FormulaInjectionPrefixesAreNeutralized(string value)
    {
        var escaped = EnterpriseReportContract.EscapeCell(value);

        Assert.StartsWith("\"'", escaped, StringComparison.Ordinal);
    }

    [Fact]
    public void B200_039_DiagnosticsManifestContainsBuildMetadataButNoEnvironmentSecrets()
    {
        const string variable = "MONITOR_B200_MANIFEST_CANARY";
        const string canary = "ManifestSecretCanary-DoNotLeak";
        var previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, canary);
        try
        {
            var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
            var bytes = EnterpriseReportContract.ManifestJson(clock);
            var json = Encoding.UTF8.GetString(bytes);
            var manifest = JsonSerializer.Deserialize<DiagnosticsBuildManifest>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.NotNull(manifest);
            Assert.Equal(EnterpriseReportContract.SchemaVersion, manifest.SchemaVersion);
            Assert.Equal("Monitor", manifest.Product);
            Assert.False(string.IsNullOrWhiteSpace(manifest.Version));
            Assert.DoesNotContain(canary, json, StringComparison.Ordinal);
            Assert.DoesNotContain(variable, json, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public void B200_040_ServerReportingUsesPeekOnlyAndReportActionsAreGetOnly()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var registration = Registration("Cache Only SQL");
        var registrations = new RegistrationStore([registration]);
        var cache = new PeekOnlyCache();
        var metadata = new InMemoryOperatorMetadataStore(clock);
        metadata.UpsertServer(new(registration.Id, ServerEnvironmentClass.Production, "core", ["tier-1"], null, null, clock.UtcNow));
        var service = Service(registrations, cache, metadata, clock: clock);

        _ = service.Servers(new());

        Assert.Equal(1, cache.PeekCalls);
        Assert.Equal(0, cache.GetCalls);
        Assert.Equal(0, cache.RefreshCalls);
        foreach (var method in typeof(EnterpriseReportsController).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(method => method.DeclaringType == typeof(EnterpriseReportsController)))
        {
            Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(method.GetCustomAttribute<HttpPostAttribute>());
        }
    }

    private static EnterpriseReportingService Service(
        IServerRegistrationRepository registrations,
        IServerHealthSnapshotCache cache,
        IOperatorMetadataStore metadata,
        IHealthIncidentRepository? incidents = null,
        ISnapshotHistoryStore? history = null,
        IAuditStore? audit = null,
        TimeProvider? clock = null) =>
        new(
            registrations,
            cache,
            metadata,
            incidents ?? new IncidentStore([]),
            history ?? new HistoryStore([]),
            audit ?? new AuditStore([]),
            clock ?? TimeProvider.System);

    private static ServerRegistration Registration(string name) => new(
        Guid.NewGuid(),
        name,
        new SqlServerEndpoint("sql.internal", 1433),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        DateTimeOffset.Parse("2026-08-11T00:00:00Z"));

    private static HealthIncident Incident(DateTimeOffset now)
    {
        var registrationId = Guid.NewGuid();
        return new($"{registrationId:N}:memory.pressure", registrationId, "memory.pressure", FindingSeverity.Warning, "Memory pressure", "Sensitive incident Evidence must not be exported.", now, now, 1, IncidentStatus.Open);
    }

    private static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class RegistrationStore(IReadOnlyList<ServerRegistration> values) : IServerRegistrationRepository
    {
        private readonly List<ServerRegistration> _values = values.ToList();
        public IReadOnlyList<ServerRegistration> GetAll() => _values.ToArray();
        public ServerRegistration? GetById(Guid id) => _values.FirstOrDefault(item => item.Id == id);
        public void Upsert(ServerRegistration registration) { _values.RemoveAll(item => item.Id == registration.Id); _values.Add(registration); }
        public bool Remove(Guid id) => _values.RemoveAll(item => item.Id == id) > 0;
    }

    private sealed class PeekOnlyCache : IServerHealthSnapshotCache
    {
        public int PeekCalls { get; private set; }
        public int GetCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public SnapshotCacheResult? Peek(Guid registrationId) { PeekCalls++; return null; }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) { GetCalls++; throw new InvalidOperationException("Report must not call GetAsync."); }
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default) { RefreshCalls++; throw new InvalidOperationException("Report must not call RefreshAsync."); }
    }

    private sealed class IncidentStore(IReadOnlyList<HealthIncident> values) : IHealthIncidentRepository
    {
        private readonly List<HealthIncident> _values = values.ToList();
        public void Apply(IEnumerable<HealthFinding> findings) => throw new NotSupportedException();
        public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve) => throw new NotSupportedException();
        public IReadOnlyList<HealthIncident> GetAll() => _values.ToArray();
        public HealthIncident? GetById(string id) => _values.FirstOrDefault(item => item.Id == id);
        public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next) => false;
    }

    private sealed class HistoryStore(IReadOnlyList<SnapshotHistoryPoint> values) : ISnapshotHistoryStore
    {
        private readonly List<SnapshotHistoryPoint> _values = values.ToList();
        public void Append(SnapshotCacheResult result) => throw new NotSupportedException();
        public IReadOnlyList<SnapshotHistoryPoint> Read(Guid registrationId, TimeSpan window) => Read(registrationId, window, 0, EnterpriseReportContract.MaxRows);
        public IReadOnlyList<SnapshotHistoryPoint> Read(Guid registrationId, TimeSpan window, int offset, int limit)
        {
            var now = DateTimeOffset.Parse("2026-08-11T00:00:00Z");
            return _values.Where(item => item.RegistrationId == registrationId && item.CollectedAtUtc >= now - window).OrderBy(item => item.CollectedAtUtc).Skip(offset).Take(limit).ToArray();
        }
    }

    private sealed class AuditStore(IReadOnlyList<AuditEvent> values) : IAuditStore
    {
        private readonly IReadOnlyList<AuditEvent> _values = values;
        public void Append(string actor, string action, string target, string outcome) => throw new NotSupportedException();
        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => _values.Skip(offset).Take(limit).ToArray();
    }
}
