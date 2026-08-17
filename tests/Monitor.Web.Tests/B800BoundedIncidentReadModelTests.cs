using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800BoundedIncidentReadModelTests
{
    private static readonly Guid ServerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ServerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultLimit_MatchesExistingIncidentScalePolicy()
    {
        Assert.Equal(BoundedIncidentReadModel.DefaultLimit, new PerformanceScaleOptions().IncidentMaxPageSize);
        Assert.Equal(100, BoundedIncidentReadModel.DefaultLimit);
    }

    [Fact]
    public void ActiveForRegistrations_FiltersServerScopeAndResolvedRowsDeterministically()
    {
        var repository = new FixedRepository([
            Incident("b", ServerA, FindingSeverity.Warning, IncidentStatus.Open, Now.AddMinutes(-1)),
            Incident("a", ServerA, FindingSeverity.Critical, IncidentStatus.Open, Now.AddMinutes(-2)),
            Incident("resolved", ServerA, FindingSeverity.Critical, IncidentStatus.Resolved, Now),
            Incident("other-server", ServerB, FindingSeverity.Critical, IncidentStatus.Open, Now)
        ]);

        var result = BoundedIncidentReadModel.ActiveForRegistrations(repository, [ServerA], limit: 10);

        Assert.True(result.IsComplete);
        Assert.False(result.IsTruncated);
        Assert.Equal(10, result.Limit);
        Assert.Equal(new[] { "a", "b" }, result.Incidents.Select(item => item.Id).ToArray());
        Assert.Equal(1, repository.ReadCount);
    }

    [Fact]
    public void ActiveForServer_ReportsOverflowInsteadOfTreatingPartialEvidenceAsComplete()
    {
        var repository = new FixedRepository([
            Incident("1", ServerA, FindingSeverity.Critical, IncidentStatus.Open, Now),
            Incident("2", ServerA, FindingSeverity.Warning, IncidentStatus.Open, Now.AddMinutes(-1)),
            Incident("3", ServerA, FindingSeverity.Warning, IncidentStatus.Acknowledged, Now.AddMinutes(-2))
        ]);

        var result = BoundedIncidentReadModel.ActiveForServer(repository, ServerA, limit: 2);

        Assert.False(result.IsComplete);
        Assert.True(result.IsTruncated);
        Assert.Equal(2, result.Incidents.Count);
        Assert.Equal(new[] { "1", "2" }, result.Incidents.Select(item => item.Id).ToArray());
        Assert.Equal(1, repository.ReadCount);
    }

    [Fact]
    public void EmptyRegistrationScope_ReturnsCompleteEmptyEvidenceWithoutReadingStore()
    {
        var repository = new FixedRepository([], throwOnRead: true);

        var result = BoundedIncidentReadModel.ActiveForRegistrations(repository, []);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Incidents);
        Assert.Equal(0, repository.ReadCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void InvalidLimit_FailsClosed(int limit)
    {
        var repository = new FixedRepository([]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BoundedIncidentReadModel.ActiveForServer(repository, ServerA, limit));
    }

    private static HealthIncident Incident(
        string id,
        Guid registrationId,
        FindingSeverity severity,
        IncidentStatus status,
        DateTimeOffset lastSeen) =>
        new(id, registrationId, "rule." + id, severity, "Title", "Evidence", lastSeen.AddMinutes(-1), lastSeen, 1, status);

    private sealed class FixedRepository(IReadOnlyList<HealthIncident> incidents, bool throwOnRead = false) : IHealthIncidentRepository
    {
        public int ReadCount { get; private set; }

        public void Apply(IEnumerable<HealthFinding> findings) => throw new NotSupportedException();
        public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve) => throw new NotSupportedException();
        public IReadOnlyList<HealthIncident> GetAll() => throw new InvalidOperationException("Bounded decision reads must not use GetAll().");

        public IncidentRepositoryReadResult Read(IncidentRepositoryQuery query)
        {
            ReadCount++;
            if (throwOnRead) throw new InvalidOperationException("Store read should not occur for empty scope.");
            return IncidentRepositoryRead.Project(incidents, query);
        }

        public HealthIncident? GetById(string id) => incidents.FirstOrDefault(item => item.Id == id);
        public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next) => false;
    }
}
