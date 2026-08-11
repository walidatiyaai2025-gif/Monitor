using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300EstateInventoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");

    [Theory]
    [InlineData("16.0.1000.6", 16, 0, 1000, 6)]
    [InlineData("17.1", 17, 1, 0, 0)]
    public void B300_061_ProductVersionParserAcceptsBoundedNumericParts(string value, int major, int minor, int build, int revision)
    {
        Assert.True(SqlProductVersion.TryParse(value, out var parsed));
        Assert.Equal(new SqlProductVersion(major, minor, build, revision), parsed);
        Assert.False(SqlProductVersion.TryParse("16.x.1", out _));
    }

    [Theory]
    [InlineData(14, SqlMajorGeneration.Pre15)]
    [InlineData(15, SqlMajorGeneration.Major15)]
    [InlineData(16, SqlMajorGeneration.Major16)]
    [InlineData(17, SqlMajorGeneration.Major17Plus)]
    [InlineData(20, SqlMajorGeneration.Major17Plus)]
    public void B300_062_MajorVersionClassificationIsPolicyNeutral(int major, SqlMajorGeneration expected)
    {
        Assert.Equal(expected, EstateInventory.Generation(new SqlProductVersion(major, 0, 0, 0)));
    }

    [Theory]
    [InlineData("Enterprise Edition", SqlEditionClass.Enterprise)]
    [InlineData("Standard", SqlEditionClass.Standard)]
    [InlineData("Express Edition", SqlEditionClass.Express)]
    [InlineData("Developer Edition", SqlEditionClass.Developer)]
    [InlineData("Web Edition", SqlEditionClass.Other)]
    public void B300_063_EditionNormalizationUsesBoundedClasses(string value, SqlEditionClass expected)
    {
        Assert.Equal(expected, EstateInventory.Edition(value));
    }

    [Fact]
    public void B300_064_InstanceTopologyDistinguishesDefaultAndNamedInstances()
    {
        Assert.Equal(SqlInstanceTopology.DefaultInstance, EstateInventory.Topology(Registration(Guid.NewGuid(), instance: null)));
        Assert.Equal(SqlInstanceTopology.NamedInstance, EstateInventory.Topology(Registration(Guid.NewGuid(), instance: "REPORTING")));
    }

    [Fact]
    public void B300_065_EncryptionPostureDistinguishesStrongTrustAndPlaintext()
    {
        Assert.Equal(EncryptionPosture.Strong, EstateInventory.Encryption(Registration(Guid.NewGuid(), encrypt: true, trust: false)));
        Assert.Equal(EncryptionPosture.EncryptedTrustsServerCertificate, EstateInventory.Encryption(Registration(Guid.NewGuid(), encrypt: true, trust: true)));
        Assert.Equal(EncryptionPosture.NotEncrypted, EstateInventory.Encryption(Registration(Guid.NewGuid(), encrypt: false, trust: false)));
    }

    [Fact]
    public void B300_066_RegistrationLifecycleRespectsDisabledFreshStaleAndUnavailable()
    {
        var id = Guid.NewGuid();
        Assert.Equal(RegistrationLifecycleState.Disabled, EstateInventory.Lifecycle(Registration(id, enabled: false), Cached(id)));
        Assert.Equal(RegistrationLifecycleState.ActiveFresh, EstateInventory.Lifecycle(Registration(id), Cached(id)));
        Assert.Equal(RegistrationLifecycleState.ActiveStale, EstateInventory.Lifecycle(Registration(id), Cached(id, SnapshotFreshness.Stale)));
        Assert.Equal(RegistrationLifecycleState.ActiveUnavailable, EstateInventory.Lifecycle(Registration(id), null));
    }

    [Fact]
    public void B300_067_DisabledOrUnavailableInventoryDoesNotInventVersionData()
    {
        var id = Guid.NewGuid();
        var row = EstateInventory.Project(Registration(id, enabled: false), null, Metadata(id), new EstateLifecyclePolicy(16));
        Assert.Null(row.Version);
        Assert.Equal(SqlMajorGeneration.Unknown, row.Generation);
        Assert.False(row.UpgradeCandidate);
    }

    [Fact]
    public void B300_068_EnvironmentVersionMatrixGroupsDeterministically()
    {
        var rows = new[]
        {
            Row(ServerEnvironmentClass.Production, SqlMajorGeneration.Major16),
            Row(ServerEnvironmentClass.Production, SqlMajorGeneration.Major16),
            Row(ServerEnvironmentClass.Test, SqlMajorGeneration.Major15)
        };
        var matrix = EstateInventory.Matrix(rows);
        Assert.Equal(2, matrix.Count);
        Assert.Contains(matrix, item => item.Environment == ServerEnvironmentClass.Production && item.Generation == SqlMajorGeneration.Major16 && item.Servers == 2);
    }

    [Fact]
    public void B300_069_UpgradeCandidateUsesExplicitMinimumMajorPolicy()
    {
        var oldId = Guid.NewGuid();
        var currentId = Guid.NewGuid();
        var old = EstateInventory.Project(Registration(oldId), Cached(oldId, version: "15.0.1.0"), Metadata(oldId), new EstateLifecyclePolicy(16));
        var current = EstateInventory.Project(Registration(currentId), Cached(currentId, version: "16.0.1.0"), Metadata(currentId), new EstateLifecyclePolicy(16));
        Assert.True(old.UpgradeCandidate);
        Assert.False(current.UpgradeCandidate);
    }

    [Fact]
    public void B300_070_EstateReadUsesPeekOnlyAndNeverCollects()
    {
        var id = Guid.NewGuid();
        var cache = new PeekOnlyCache(id, Cached(id));
        var service = new EstateInventoryService(new RegistrationStore([Registration(id)]), cache, new MetadataStore(Metadata(id)), new EstateLifecyclePolicy(16));
        var row = Assert.Single(service.Read());
        Assert.Equal(id, row.RegistrationId);
        Assert.Equal(1, cache.Peeks);
        Assert.Equal(0, cache.Collections);
    }

    private static EstateInventoryRow Row(ServerEnvironmentClass environment, SqlMajorGeneration generation) =>
        new(Guid.NewGuid(), environment, new SqlProductVersion(generation == SqlMajorGeneration.Major15 ? 15 : 16, 0, 0, 0), generation, SqlEditionClass.Enterprise, SqlInstanceTopology.DefaultInstance, EncryptionPosture.Strong, RegistrationLifecycleState.ActiveFresh, false);

    private static ServerRegistration Registration(Guid id, bool enabled = true, string? instance = null, bool encrypt = true, bool trust = false) =>
        new(id, "SQL", new SqlServerEndpoint("sql.internal", 1433, instance, encrypt, trust), SqlAuthenticationMode.IntegratedSecurity, null, enabled, Now);

    private static ServerOperatorMetadata Metadata(Guid id) => new(id, ServerEnvironmentClass.Production, "core", [], null, null, Now);

    private static SnapshotCacheResult Cached(Guid id, SnapshotFreshness freshness = SnapshotFreshness.Fresh, string version = "16.0.1000.6") =>
        new(new ServerHealthSnapshot(id, "SQL", version, "Enterprise Edition", null, 3600, 10, 10, Now), freshness, freshness == SnapshotFreshness.Fresh ? TimeSpan.FromSeconds(5) : TimeSpan.FromMinutes(2));

    private sealed class RegistrationStore(IReadOnlyList<ServerRegistration> values) : IServerRegistrationRepository
    {
        public IReadOnlyList<ServerRegistration> GetAll() => values;
        public ServerRegistration? GetById(Guid id) => values.FirstOrDefault(item => item.Id == id);
        public void Upsert(ServerRegistration registration) => throw new NotSupportedException();
        public bool Remove(Guid id) => false;
    }
    private sealed class MetadataStore(ServerOperatorMetadata value) : IOperatorMetadataStore
    {
        public ServerOperatorMetadata GetServer(Guid registrationId) => value;
        public void UpsertServer(ServerOperatorMetadata metadata) => throw new NotSupportedException();
        public IncidentOperatorMetadata GetIncident(string incidentId) => InMemoryOperatorMetadataStore.EmptyIncident(incidentId, Now);
        public void AssignIncident(string incidentId, string? assignee) => throw new NotSupportedException();
        public void AddIncidentNote(string incidentId, string actor, string note) => throw new NotSupportedException();
        public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged) => throw new NotSupportedException();
        public EnterpriseOperatorSnapshot Snapshot() => new([value], []);
    }
    private sealed class PeekOnlyCache(Guid id, SnapshotCacheResult value) : IServerHealthSnapshotCache
    {
        public int Peeks { get; private set; }
        public int Collections { get; private set; }
        public SnapshotCacheResult? Peek(Guid registrationId) { Peeks++; return registrationId == id ? value : null; }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) { Collections++; throw new InvalidOperationException(); }
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default) { Collections++; throw new InvalidOperationException(); }
    }
}
