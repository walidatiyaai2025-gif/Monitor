using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class LoginAttemptLimiterCapacityTests
{
    [Fact]
    public void ActiveStateCapacity_FailsClosedForNewKeys_ThenReclaimsExpiredEntries()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.Zero));
        var limiter = new LoginAttemptLimiter(time);

        for (var index = 0; index < LoginAttemptLimiter.MaxTrackedKeys; index++)
        {
            var key = $"opaque-{index:D4}";
            Assert.True(limiter.IsAllowed(key));
            limiter.RecordFailure(key);
        }

        Assert.Equal(LoginAttemptLimiter.MaxTrackedKeys, limiter.TrackedKeyCount);
        Assert.False(limiter.IsAllowed("opaque-overflow"));
        limiter.RecordFailure("opaque-overflow");
        Assert.Equal(LoginAttemptLimiter.MaxTrackedKeys, limiter.TrackedKeyCount);

        time.Advance(LoginAttemptLimiter.Window + TimeSpan.FromSeconds(1));

        Assert.True(limiter.IsAllowed("opaque-after-expiry"));
        Assert.Equal(0, limiter.TrackedKeyCount);
        limiter.RecordFailure("opaque-after-expiry");
        Assert.Equal(1, limiter.TrackedKeyCount);
    }

    [Fact]
    public void ExistingTrackedKey_KeepsFiveFailureWindowSemantics()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.Zero));
        var limiter = new LoginAttemptLimiter(time);
        const string key = "opaque-existing";

        for (var index = 0; index < LoginAttemptLimiter.FailureLimit; index++)
        {
            Assert.True(limiter.IsAllowed(key));
            limiter.RecordFailure(key);
        }

        Assert.False(limiter.IsAllowed(key));
        time.Advance(LoginAttemptLimiter.Window + TimeSpan.FromSeconds(1));
        Assert.True(limiter.IsAllowed(key));
        Assert.Equal(0, limiter.TrackedKeyCount);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
