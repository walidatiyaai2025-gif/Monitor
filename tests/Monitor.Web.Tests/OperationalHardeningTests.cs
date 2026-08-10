using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class OperationalHardeningTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Backoff_UsesBoundedExponentialDelayAndSuccessResets()
    {
        var clock = new MutableClock(Now);
        var policy = new CollectionBackoffPolicy(clock);
        var id = Guid.NewGuid();
        policy.Failure(id);
        Assert.False(policy.IsEligible(id));
        clock.Now = Now.AddSeconds(30);
        Assert.True(policy.IsEligible(id));
        policy.Failure(id);
        clock.Now = Now.AddSeconds(89);
        Assert.False(policy.IsEligible(id));
        clock.Now = Now.AddSeconds(90);
        Assert.True(policy.IsEligible(id));
        policy.Success(id);
        Assert.True(policy.IsEligible(id));
    }

    [Fact]
    public void Audit_IsBoundedOrderedAndTruncated()
    {
        var store = new InMemoryAuditStore(new MutableClock(Now));
        for (var i = 0; i < 1005; i++) store.Append(new string('a', 150), "action", $"target-{i}", "success");
        var page = store.Read(0, 100);
        Assert.Equal(100, page.Count);
        Assert.All(page, item => Assert.True(item.Actor.Length <= 100));
        Assert.DoesNotContain(page, item => item.Target == "target-0");
    }

    [Fact]
    public void LoginLimiter_BlocksFifthFailureAndResetsAfterWindow()
    {
        var clock = new MutableClock(Now);
        var limiter = new LoginAttemptLimiter(clock);
        for (var i = 0; i < 5; i++) limiter.RecordFailure("partition");
        Assert.False(limiter.IsAllowed("partition"));
        Assert.True(limiter.IsAllowed("other"));
        clock.Now = Now.AddMinutes(5);
        Assert.True(limiter.IsAllowed("partition"));
    }

    [Fact]
    public async Task AdvisorRequests_AreSingleFlightAndCachedByEvidenceVersion()
    {
        var repository = new InMemoryHealthIncidentRepository();
        var id = Guid.NewGuid();
        repository.Apply([new(id, "backup.full-gap", FindingSeverity.Warning, "Gap", "2 missing", Now)]);
        var incident = Assert.Single(repository.GetAll());
        var provider = new CountingAdvisorProvider();
        var service = new AdvisorRequestService(repository, new RecommendationEngine(), new AdvisorContextBuilder(), provider, new InMemoryAuditStore(new MutableClock(Now)), new MutableClock(Now));

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => service.RequestAsync(incident.Id, "operator", default)));
        var cached = await service.RequestAsync(incident.Id, "operator", default);

        Assert.All(results, result => Assert.Equal(AdvisorStatus.Ready, result.Status));
        Assert.Equal(AdvisorStatus.Ready, cached.Status);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void SchedulerDefaultsToDisabledAndStatusIsImmutableSnapshot()
    {
        var options = new SnapshotScheduleOptions();
        options.Validate();
        Assert.False(options.Enabled);
        var store = new SchedulerStatusStore();
        var status = new SchedulerStatus(false, false, null, null, 0, 0, 0, 0);
        store.Set(status);
        Assert.Equal(status, store.Get());
    }

    private sealed class CountingAdvisorProvider : IAdvisorProvider
    {
        private int _calls;
        public int CallCount => _calls;
        public async Task<AdvisorResult> AdviseAsync(AdvisorContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(20, cancellationToken);
            return new(AdvisorStatus.Ready, "Advisory result");
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
