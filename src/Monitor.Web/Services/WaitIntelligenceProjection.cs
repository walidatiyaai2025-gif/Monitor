using Monitor.Web.Models;

namespace Monitor.Web.Services;

public static class WaitIntelligenceProjection
{
    public static IReadOnlyList<WaitIntelligence> Build(
        PerformanceHealthSnapshot? performance,
        long? uptimeSeconds,
        int limit = 8)
    {
        if (performance?.Waits is not { Count: > 0 } waits || uptimeSeconds is null or <= 0)
        {
            return [];
        }

        var interval = TimeSpan.FromSeconds(uptimeSeconds.Value);
        var samples = waits.Select(wait => new WaitSample(
            wait.WaitType,
            wait.WaitTimeMs,
            wait.SignalWaitTimeMs,
            wait.WaitingTasks,
            interval));

        return Batch400WaitIntelligence.Summarize(samples, Math.Clamp(limit, 1, 20));
    }
}
