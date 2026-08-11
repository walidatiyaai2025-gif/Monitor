namespace Monitor.Web.Services;

public enum DatabaseStateClass
{
    Unknown,
    Online,
    Restoring,
    Recovering,
    RecoveryPending,
    Suspect,
    Emergency,
    Offline
}

public sealed record DatabaseStateSummary(int Online, int Restoring, int Recovering, int RecoveryPending, int Suspect, int Emergency, int Offline, int Unknown)
{
    public int Total => Online + Restoring + Recovering + RecoveryPending + Suspect + Emergency + Offline + Unknown;
}

public static class Batch300DatabaseState
{
    public static string NormalizeState(string? state) => string.Join(' ', (state ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static DatabaseStateClass Classify(string? state) => NormalizeState(state) switch
    {
        "ONLINE" => DatabaseStateClass.Online,
        "RESTORING" => DatabaseStateClass.Restoring,
        "RECOVERING" => DatabaseStateClass.Recovering,
        "RECOVERY_PENDING" or "RECOVERY PENDING" => DatabaseStateClass.RecoveryPending,
        "SUSPECT" => DatabaseStateClass.Suspect,
        "EMERGENCY" => DatabaseStateClass.Emergency,
        "OFFLINE" => DatabaseStateClass.Offline,
        _ => DatabaseStateClass.Unknown
    };

    public static bool IsOnline(string? state) => Classify(state) == DatabaseStateClass.Online;

    public static bool IsActionable(DatabaseStateClass state) => state is DatabaseStateClass.RecoveryPending or DatabaseStateClass.Suspect or DatabaseStateClass.Emergency or DatabaseStateClass.Offline;

    public static int AvailabilityScore(IEnumerable<string?> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        var values = states.Select(Classify).ToArray();
        if (values.Length == 0) return 100;
        var online = values.Count(state => state == DatabaseStateClass.Online);
        return (int)Math.Round(online * 100d / values.Length, MidpointRounding.AwayFromZero);
    }

    public static int CountUnavailable(IEnumerable<string?> states) => states.Count(state => Classify(state) != DatabaseStateClass.Online);

    public static int CountRestoring(IEnumerable<string?> states) => states.Count(state => Classify(state) == DatabaseStateClass.Restoring);

    public static DatabaseStateClass Worst(IEnumerable<string?> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        static int Rank(DatabaseStateClass state) => state switch
        {
            DatabaseStateClass.Suspect => 8,
            DatabaseStateClass.RecoveryPending => 7,
            DatabaseStateClass.Emergency => 6,
            DatabaseStateClass.Offline => 5,
            DatabaseStateClass.Recovering => 4,
            DatabaseStateClass.Restoring => 3,
            DatabaseStateClass.Unknown => 2,
            DatabaseStateClass.Online => 1,
            _ => 0
        };
        return states.Select(Classify).OrderByDescending(Rank).FirstOrDefault(DatabaseStateClass.Unknown);
    }

    public static bool FailoverReady(IEnumerable<string?> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        var values = states.Select(Classify).ToArray();
        return values.Length > 0 && values.All(state => state == DatabaseStateClass.Online);
    }

    public static DatabaseStateSummary Summarize(IEnumerable<string?> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        var values = states.Select(Classify).ToArray();
        return new(
            values.Count(state => state == DatabaseStateClass.Online),
            values.Count(state => state == DatabaseStateClass.Restoring),
            values.Count(state => state == DatabaseStateClass.Recovering),
            values.Count(state => state == DatabaseStateClass.RecoveryPending),
            values.Count(state => state == DatabaseStateClass.Suspect),
            values.Count(state => state == DatabaseStateClass.Emergency),
            values.Count(state => state == DatabaseStateClass.Offline),
            values.Count(state => state == DatabaseStateClass.Unknown));
    }
}
