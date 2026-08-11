namespace Monitor.Web.Services;

public enum ReplicaSyncState { Unknown, Synchronized, Synchronizing, NotSynchronizing, Reverting, Initializing }
public enum HaLagBand { None, Low, Elevated, High, Critical }
public sealed record HaReplicaSample(string State, double SendQueueMb, double RedoQueueMb, double LagSeconds, bool Connected, bool AutomaticFailover);
public sealed record HaReadinessSummary(ReplicaSyncState State, HaLagBand LagBand, double QueueScore, double SyncScore, bool FailoverReady, bool RpoCompliant, bool RtoReady, bool QuorumRisk, double Score, B400Severity Severity, string Reason);

public static class Batch400HaReadiness
{
    public static ReplicaSyncState NormalizeState(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Replace(' ', '_').ToUpperInvariant();
        return normalized switch
        {
            "SYNCHRONIZED" => ReplicaSyncState.Synchronized,
            "SYNCHRONIZING" => ReplicaSyncState.Synchronizing,
            "NOT_SYNCHRONIZING" => ReplicaSyncState.NotSynchronizing,
            "REVERTING" => ReplicaSyncState.Reverting,
            "INITIALIZING" => ReplicaSyncState.Initializing,
            _ => ReplicaSyncState.Unknown
        };
    }

    public static HaLagBand LagBand(double lagSeconds)
    {
        var lag = double.IsFinite(lagSeconds) ? Math.Max(0, lagSeconds) : double.MaxValue;
        return lag switch
        {
            <= 1 => HaLagBand.None,
            <= 10 => HaLagBand.Low,
            <= 30 => HaLagBand.Elevated,
            <= 120 => HaLagBand.High,
            _ => HaLagBand.Critical
        };
    }

    public static double QueueScore(double sendQueueMb, double redoQueueMb)
    {
        var send = double.IsFinite(sendQueueMb) ? Math.Max(0, sendQueueMb) : 100_000;
        var redo = double.IsFinite(redoQueueMb) ? Math.Max(0, redoQueueMb) : 100_000;
        return Math.Round(Math.Clamp(Math.Max(send, redo) / 10d, 0, 100), 2);
    }

    public static double SyncScore(string? state, bool connected)
    {
        if (!connected) return 100;
        return NormalizeState(state) switch
        {
            ReplicaSyncState.Synchronized => 0,
            ReplicaSyncState.Synchronizing => 30,
            ReplicaSyncState.Initializing => 50,
            ReplicaSyncState.Reverting => 70,
            ReplicaSyncState.NotSynchronizing => 100,
            _ => 80
        };
    }

    public static bool FailoverReady(HaReplicaSample sample, bool quorumHealthy) => quorumHealthy && sample.Connected && NormalizeState(sample.State) == ReplicaSyncState.Synchronized && LagBand(sample.LagSeconds) is HaLagBand.None or HaLagBand.Low && QueueScore(sample.SendQueueMb, sample.RedoQueueMb) < 30;

    public static bool RpoCompliant(double lagSeconds, double maxLagSeconds)
    {
        if (!double.IsFinite(maxLagSeconds) || maxLagSeconds < 0) throw new ArgumentOutOfRangeException(nameof(maxLagSeconds));
        return double.IsFinite(lagSeconds) && Math.Max(0, lagSeconds) <= maxLagSeconds;
    }

    public static bool RtoReady(HaReplicaSample sample, bool quorumHealthy) => quorumHealthy && sample.Connected && NormalizeState(sample.State) is ReplicaSyncState.Synchronized or ReplicaSyncState.Synchronizing;

    public static bool QuorumRisk(int healthyVotes, int totalVotes)
    {
        if (totalVotes <= 0) return true;
        var healthy = Math.Clamp(healthyVotes, 0, totalVotes);
        return healthy <= totalVotes / 2;
    }

    public static B400Severity Severity(double score) => score switch
    {
        >= 80 => B400Severity.Critical,
        >= 45 => B400Severity.Warning,
        > 0 => B400Severity.Info,
        _ => B400Severity.None
    };

    public static HaReadinessSummary Summarize(HaReplicaSample sample, bool quorumHealthy, int healthyVotes, int totalVotes, double maxRpoLagSeconds)
    {
        var state = NormalizeState(sample.State);
        var lag = LagBand(sample.LagSeconds);
        var queue = QueueScore(sample.SendQueueMb, sample.RedoQueueMb);
        var sync = SyncScore(sample.State, sample.Connected);
        var quorumRisk = !quorumHealthy || QuorumRisk(healthyVotes, totalVotes);
        var rpo = RpoCompliant(sample.LagSeconds, maxRpoLagSeconds);
        var rto = RtoReady(sample, quorumHealthy);
        var score = Math.Round(Math.Clamp(sync * 0.35 + queue * 0.25 + (rpo ? 0 : 100) * 0.2 + (quorumRisk ? 100 : 0) * 0.2, 0, 100), 2);
        var ready = FailoverReady(sample, quorumHealthy) && !quorumRisk;
        var severity = Severity(score);
        var reason = quorumRisk ? "Quorum readiness is degraded." : !sample.Connected ? "Replica connectivity is unavailable." : !rpo ? "Replica lag exceeds the configured RPO." : !ready ? "Replica is not yet failover-ready." : "Replica satisfies current failover-readiness checks.";
        return new(state, lag, queue, sync, ready, rpo, rto, quorumRisk, score, severity, reason);
    }
}
