using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record ServerIntelligenceViewModel(
    string DisplayLabel,
    int? MajorVersion,
    string VersionFamily,
    bool? SupportedMajor,
    SqlEditionClass EditionClass,
    UptimeBand UptimeBand,
    RuntimePressureResult? RuntimePressure)
{
    public string VersionSupportLabel => SupportedMajor switch
    {
        true => "Supported baseline",
        false => "Legacy / below baseline",
        _ => "Not collected"
    };

    public string RuntimeSignalsLabel => RuntimePressure is null
        ? "Not collected"
        : RuntimePressure.Signals.Length == 0
            ? "No active pressure domains"
            : string.Join(", ", RuntimePressure.Signals);
}

public static class ServerIntelligenceProjection
{
    public static ServerIntelligenceViewModel Build(ServerDetailsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var server = model.Server;
        var evidence = model.Evidence;
        var majorVersion = Batch300EstateIdentity.MajorVersion(server.Version);
        RuntimePressureResult? runtimePressure = null;

        if (evidence?.Memory is { } memory &&
            evidence.Blocking is { } blocking &&
            evidence.Performance is { } performance)
        {
            runtimePressure = Batch300RuntimePressure.Evaluate(new RuntimePressureInput(
                memory.SqlProcessMemoryUtilizationPercent,
                blocking.BlockedRequests,
                blocking.MaxWaitMilliseconds,
                performance.RunnableTasks,
                performance.PendingIoRequests));
        }

        return new ServerIntelligenceViewModel(
            Batch300EstateIdentity.SafeDisplayLabel(server.Name, evidence?.InstanceName ?? server.InstanceName),
            majorVersion,
            majorVersion.HasValue ? Batch300EstateIdentity.VersionFamily(majorVersion.Value) : "unknown",
            majorVersion.HasValue ? Batch300EstateIdentity.IsSupportedMajor(majorVersion.Value) : null,
            Batch300EstateIdentity.ClassifyEdition(server.Edition),
            evidence is null ? UptimeBand.Unknown : Batch300EstateIdentity.ClassifyUptime(evidence.UptimeSeconds),
            runtimePressure);
    }
}
