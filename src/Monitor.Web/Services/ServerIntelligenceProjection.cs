using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum ServerIntelligenceEvidenceState
{
    Fresh,
    Stale,
    Unavailable,
    Development
}

public sealed record ServerIntelligenceViewModel(
    string DisplayLabel,
    int? MajorVersion,
    string VersionFamily,
    bool? SupportedMajor,
    SqlEditionClass EditionClass,
    UptimeBand UptimeBand,
    ServerIntelligenceEvidenceState EvidenceState,
    RuntimePressureResult? RuntimePressure)
{
    public string VersionSupportLabel => SupportedMajor switch
    {
        true => "Supported baseline",
        false => "Legacy / below baseline",
        _ => "Not collected"
    };

    public string EvidenceStateLabel => EvidenceState switch
    {
        ServerIntelligenceEvidenceState.Fresh => "Fresh cached evidence",
        ServerIntelligenceEvidenceState.Stale => "Stale cached evidence",
        ServerIntelligenceEvidenceState.Unavailable => "Unavailable",
        _ => "Development data"
    };

    public string RuntimePressureStatusLabel => RuntimePressure is null
        ? "Unavailable"
        : EvidenceState switch
        {
            ServerIntelligenceEvidenceState.Stale => $"{RuntimePressure.Classification} · stale evidence",
            ServerIntelligenceEvidenceState.Development => $"{RuntimePressure.Classification} · development data",
            ServerIntelligenceEvidenceState.Fresh => RuntimePressure.Classification.ToString(),
            _ => "Unavailable"
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
        var evidenceState = ClassifyEvidenceState(server.Source, model.Evidence);
        var evidence = evidenceState == ServerIntelligenceEvidenceState.Unavailable ? null : model.Evidence;
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
            evidenceState,
            runtimePressure);
    }

    private static ServerIntelligenceEvidenceState ClassifyEvidenceState(ServerDataSource source, ServerSnapshotEvidence? evidence) => source switch
    {
        ServerDataSource.LiveFresh when evidence is not null => ServerIntelligenceEvidenceState.Fresh,
        ServerDataSource.LiveStale when evidence is not null => ServerIntelligenceEvidenceState.Stale,
        ServerDataSource.Demo => ServerIntelligenceEvidenceState.Development,
        _ => ServerIntelligenceEvidenceState.Unavailable
    };
}
