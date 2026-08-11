using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300ReadApiTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-11T06:00:00Z");
    private static readonly Guid ServerId = Guid.Parse("fedcba98-7654-3210-aaaa-bbbbbbbbbbbb");

    [Fact]
    public void B300_071_FleetDtoIsVersionedAndContainsNoConnectionEndpoint()
    {
        var response = new DbaFleetApiResponse(DbaReadApiContract.SchemaVersion, new(0, 50, 0), []);
        Assert.Equal("monitor-dba-v1", response.SchemaVersion);
        var names = typeof(DbaFleetApiItem).GetProperties().Select(item => item.Name).ToArray();
        Assert.DoesNotContain("Host", names);
        Assert.DoesNotContain("Port", names);
        Assert.DoesNotContain("SecretReference", names);
    }

    [Fact]
    public void B300_072_RiskDtoContainsScoreActionabilityAndNoEvidence()
    {
        var names = typeof(DbaRiskApiItem).GetProperties().Select(item => item.Name).ToArray();
        Assert.Contains("Score", names);
        Assert.Contains("Actionable", names);
        Assert.DoesNotContain("Evidence", names);
        Assert.DoesNotContain("ConnectionString", names);
    }

    [Fact]
    public void B300_073_IncidentPriorityDtoExcludesEvidenceAndOperatorNotes()
    {
        var names = typeof(DbaIncidentPriorityApiItem).GetProperties().Select(item => item.Name).ToArray();
        Assert.Contains("RuleFamily", names);
        Assert.Contains("Score", names);
        Assert.DoesNotContain("Evidence", names);
        Assert.DoesNotContain("Notes", names);
    }

    [Fact]
    public void B300_074_PaginationClampsOffsetAndLimit()
    {
        Assert.Equal((0, 50), DbaReadApiContract.Page(-5, 0));
        Assert.Equal((10, 100), DbaReadApiContract.Page(10, 999));
        Assert.Equal((0, 1), DbaReadApiContract.Page(0, 1));
    }

    [Theory]
    [InlineData("Production", "Production")]
    [InlineData(" DBA-OnCall ", "DBA-OnCall")]
    [InlineData("tier_1", "tier_1")]
    public void B300_075_FilterNormalizationAllowsOnlyBoundedAsciiTokens(string input, string expected)
    {
        Assert.Equal(expected, DbaReadApiContract.NormalizeFilter(input));
        Assert.Throws<ArgumentException>(() => DbaReadApiContract.NormalizeFilter("bad filter with spaces"));
        Assert.Throws<ArgumentException>(() => DbaReadApiContract.NormalizeFilter("<script>"));
    }

    [Fact]
    public void B300_076_ETagIsStableQuotedSha256()
    {
        var value = new DbaRiskApiResponse(DbaReadApiContract.SchemaVersion, new(0, 50, 0), []);
        var first = DbaReadApiContract.ETag(value);
        var second = DbaReadApiContract.ETag(value);
        Assert.Equal(first, second);
        Assert.StartsWith('"', first);
        Assert.EndsWith('"', first);
        Assert.Equal(66, first.Length);
    }

    [Fact]
    public void B300_077_ReadApiHeadersRequirePrivateRevalidationAndNosniff()
    {
        var context = new DefaultHttpContext();
        DbaReadApiContract.ApplyHeaders(context.Response, "\"abc\"");
        Assert.Equal("private, max-age=15, must-revalidate", context.Response.Headers.CacheControl);
        Assert.Equal("\"abc\"", context.Response.Headers.ETag);
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
    }

    [Fact]
    public void B300_078_ApiContractsContainNoSecretOrConnectionProperties()
    {
        var types = new[] { typeof(DbaFleetApiItem), typeof(DbaRiskApiItem), typeof(DbaIncidentPriorityApiItem) };
        var forbidden = new[] { "Secret", "Password", "Credential", "Host", "Connection", "Evidence", "SqlText" };
        foreach (var type in types)
        {
            var names = type.GetProperties().Select(item => item.Name).ToArray();
            Assert.DoesNotContain(names, name => forbidden.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [Fact]
    public void B300_079_ReadApiControllerUsesNamedReadPolicyAndOnlyGetActions()
    {
        var authorization = Assert.Single(typeof(DbaReadApiController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(MonitorPolicies.Read, authorization.Policy);
        var actions = typeof(DbaReadApiController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.DeclaringType == typeof(DbaReadApiController))
            .ToArray();
        Assert.All(actions, action => Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>()));
    }

    [Fact]
    public void B300_080_FleetReadApiUsesCachePeekOnlyAndNeverCollects()
    {
        var cache = new PeekOnlyCache(Cached());
        var controller = new DbaReadApiController(
            new RegistrationStore([Registration()]), cache, new IncidentStore([]), new MetadataStore(Metadata()), new FixedTimeProvider(Now));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var result = controller.Fleet();
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DbaFleetApiResponse>(ok.Value);
        Assert.Single(response.Items);
        Assert.Equal(1, cache.Peeks);
        Assert.Equal(0, cache.Collections);
    }

    private static ServerRegistration Registration() => new(ServerId, "SQL-API", new SqlServerEndpoint("sql.internal", 1433), SqlAuthenticationMode.IntegratedSecurity, null, true, Now);
    private static ServerOperatorMetadata Metadata() => new(ServerId, ServerEnvironmentClass.Production, "core", [], null, null, Now);
    private static SnapshotCacheResult Cached() => new(new ServerHealthSnapshot(ServerId, "SQL-API", "16.0.1.0", "Enterprise", null, 3600, 10, 10, Now), SnapshotFreshness.Fresh, TimeSpan.FromSeconds(5));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class RegistrationStore(IReadOnlyList<ServerRegistration> values) : IServerRegistrationRepository { public IReadOnlyList<ServerRegistration> GetAll()=>values; public ServerRegistration? GetById(Guid id)=>values.FirstOrDefault(item=>item.Id==id); public void Upsert(ServerRegistration registration)=>throw new NotSupportedException(); public bool Remove(Guid id)=>false; }
    private sealed class IncidentStore(IReadOnlyList<HealthIncident> values) : IHealthIncidentRepository { public void Apply(IEnumerable<HealthFinding> findings)=>throw new NotSupportedException(); public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve)=>throw new NotSupportedException(); public IReadOnlyList<HealthIncident> GetAll()=>values; public HealthIncident? GetById(string id)=>values.FirstOrDefault(item=>item.Id==id); public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next)=>false; }
    private sealed class MetadataStore(ServerOperatorMetadata server) : IOperatorMetadataStore { public ServerOperatorMetadata GetServer(Guid registrationId)=>server; public void UpsertServer(ServerOperatorMetadata metadata)=>throw new NotSupportedException(); public IncidentOperatorMetadata GetIncident(string incidentId)=>InMemoryOperatorMetadataStore.EmptyIncident(incidentId,Now); public void AssignIncident(string incidentId,string? assignee)=>throw new NotSupportedException(); public void AddIncidentNote(string incidentId,string actor,string note)=>throw new NotSupportedException(); public void SetRecommendationAcknowledged(string incidentId,string recommendationKey,bool acknowledged)=>throw new NotSupportedException(); public EnterpriseOperatorSnapshot Snapshot()=>new([server],[]); }
    private sealed class PeekOnlyCache(SnapshotCacheResult value) : IServerHealthSnapshotCache { public int Peeks{get;private set;} public int Collections{get;private set;} public SnapshotCacheResult? Peek(Guid registrationId){Peeks++;return value;} public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration,CancellationToken cancellationToken=default){Collections++;throw new InvalidOperationException();} public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration,CancellationToken cancellationToken=default){Collections++;throw new InvalidOperationException();} }
}
