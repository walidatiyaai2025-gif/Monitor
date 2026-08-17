using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record MemoryIntelligenceViewModel(
    string State,
    string Recommendation,
    int? TargetAttainmentPercent,
    long? OsHeadroomMb,
    string TopMemoryClerkLabel)
{
    public bool NeedsAttention => State is "warning" or "critical";
}

public static class MemoryIntelligenceProjection
{
    public static MemoryIntelligenceViewModel Build(MemoryHealthSnapshot? memory)
    {
        if (memory is null)
        {
            return new(
                "unknown",
                "Memory diagnostic evidence is not collected for this snapshot. Refresh the bounded snapshot after connectivity and permissions are validated; do not infer zero pressure.",
                null,
                null,
                "Not collected");
        }

        var targetAttainment = memory.TotalServerMemoryKb.HasValue && memory.TargetServerMemoryKb is > 0
            ? (int?)Math.Clamp((int)Math.Round(memory.TotalServerMemoryKb.Value * 100d / memory.TargetServerMemoryKb.Value), 0, 1000)
            : null;
        long? osHeadroomMb = memory.AvailablePhysicalMemoryKb >= 0
            ? memory.AvailablePhysicalMemoryKb / 1024
            : null;
        var topClerk = !string.IsNullOrWhiteSpace(memory.TopMemoryClerkType) && memory.TopMemoryClerkKb.HasValue
            ? $"{memory.TopMemoryClerkType} · {memory.TopMemoryClerkKb.Value / 1024d:0.0} MB"
            : "Not collected";

        if (memory.IsPhysicalMemoryLow || memory.IsVirtualMemoryLow)
        {
            return new(
                "critical",
                "SQL Server or the host reports low-memory pressure. Review max server memory against OS headroom and workload under change control; do not apply an automatic configuration change.",
                targetAttainment,
                osHeadroomMb,
                topClerk);
        }

        if (memory.MemoryGrantsPending is > 0)
        {
            return new(
                "warning",
                "Memory grants are pending. Investigate grant contention and workload shape using approved DBA diagnostics before changing memory or query configuration.",
                targetAttainment,
                osHeadroomMb,
                topClerk);
        }

        if (memory.SqlProcessMemoryUtilizationPercent >= 85)
        {
            return new(
                "warning",
                "SQL process memory utilization is elevated. Compare max server memory, OS headroom, Total/Target Server Memory and the dominant memory clerk before deciding on a controlled change.",
                targetAttainment,
                osHeadroomMb,
                topClerk);
        }

        if (targetAttainment is < 80 && memory.SqlProcessMemoryUtilizationPercent >= 75)
        {
            return new(
                "warning",
                "SQL memory is below its reported target while process utilization is elevated. Review workload pressure and trend evidence; the snapshot does not authorize automatic tuning.",
                targetAttainment,
                osHeadroomMb,
                topClerk);
        }

        return new(
            "healthy",
            "No collected memory pressure threshold is currently crossed. Continue trend review; PLE is shown as evidence and should be interpreted against the server workload baseline rather than a universal fixed threshold.",
            targetAttainment,
            osHeadroomMb,
            topClerk);
    }
}
