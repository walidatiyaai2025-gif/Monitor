using System.Globalization;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public static class MemoryHealthSummaryExport
{
    private static readonly IReadOnlyList<string> Headers = ["Section", "Metric", "Value"];

    public static byte[] Build(ServerDetailsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var evidence = model.Evidence;
        var memory = evidence?.Memory;
        var projection = MemoryIntelligenceProjection.Build(memory);
        var rows = new List<IReadOnlyList<string?>>();

        Add(rows, "Evidence", "State", model.Server.Source switch
        {
            ServerDataSource.LiveFresh => "Fresh",
            ServerDataSource.LiveStale => "Stale",
            _ => "Unavailable"
        });
        Add(rows, "Evidence", "CollectedAtUtc", evidence?.CollectedAtUtc.ToString("O", CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Server", "DisplayLabel", model.Server.Name);

        Add(rows, "Memory", "State", memory is null ? "Unavailable" : "Available");
        Add(rows, "Memory", "PressureState", memory is null ? "Unavailable" : projection.State);
        Add(rows, "Memory", "NeedsAttention", memory is null ? "Unavailable" : projection.NeedsAttention.ToString(CultureInfo.InvariantCulture));
        Add(rows, "Memory", "SqlProcessUtilizationPercent", memory?.SqlProcessMemoryUtilizationPercent.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Memory", "SqlProcessPhysicalMemoryMb", memory is null ? "Unavailable" : FormatMbFromKb(memory.SqlProcessPhysicalMemoryKb));

        Add(rows, "OS", "TotalPhysicalMemoryMb", memory is null ? "Unavailable" : FormatMbFromKb(memory.TotalPhysicalMemoryKb));
        Add(rows, "OS", "AvailablePhysicalMemoryMb", memory is null ? "Unavailable" : FormatMbFromKb(memory.AvailablePhysicalMemoryKb));
        Add(rows, "OS", "HeadroomMb", memory is null ? "Unavailable" : FormatNullable(projection.OsHeadroomMb));
        Add(rows, "OS", "PhysicalMemoryLow", memory is null ? "Unavailable" : memory.IsPhysicalMemoryLow.ToString(CultureInfo.InvariantCulture));
        Add(rows, "OS", "VirtualMemoryLow", memory is null ? "Unavailable" : memory.IsVirtualMemoryLow.ToString(CultureInfo.InvariantCulture));
        Add(rows, "OS", "SystemMemoryState", memory?.SystemMemoryState ?? "Unavailable");

        Add(rows, "Configuration", "MaxServerMemoryMb", memory is null ? "Unavailable" : FormatNullable(memory.MaxServerMemoryMb));
        Add(rows, "Counters", "TotalServerMemoryMb", memory is null ? "Unavailable" : FormatNullableMbFromKb(memory.TotalServerMemoryKb));
        Add(rows, "Counters", "TargetServerMemoryMb", memory is null ? "Unavailable" : FormatNullableMbFromKb(memory.TargetServerMemoryKb));
        Add(rows, "Counters", "TargetAttainmentPercent", memory is null ? "Unavailable" : FormatNullable(projection.TargetAttainmentPercent));
        Add(rows, "Counters", "PageLifeExpectancySeconds", memory is null ? "Unavailable" : FormatNullable(memory.PageLifeExpectancySeconds));
        Add(rows, "Counters", "MemoryGrantsPending", memory is null ? "Unavailable" : FormatNullable(memory.MemoryGrantsPending));
        Add(rows, "Clerk", "Dominant", memory is null ? "Unavailable" : projection.TopMemoryClerkLabel);

        return EnterpriseReportContract.Csv(Headers, rows);
    }

    private static string FormatMbFromKb(long value) => (value / 1024d).ToString("0.0", CultureInfo.InvariantCulture);

    private static string FormatNullableMbFromKb(long? value) =>
        value.HasValue ? FormatMbFromKb(value.Value) : "Unavailable";

    private static string FormatNullable(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable";

    private static string FormatNullable(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable";

    private static void Add(List<IReadOnlyList<string?>> rows, string section, string metric, string value) =>
        rows.Add([section, metric, value]);
}
