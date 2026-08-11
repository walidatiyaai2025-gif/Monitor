using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum SqlMajorGeneration
{
    Unknown,
    Pre15,
    Major15,
    Major16,
    Major17Plus
}

public enum SqlEditionClass
{
    Unknown,
    Enterprise,
    Standard,
    Express,
    Developer,
    Other
}

public enum SqlInstanceTopology
{
    DefaultInstance,
    NamedInstance
}

public enum EncryptionPosture
{
    Strong,
    EncryptedTrustsServerCertificate,
    NotEncrypted
}

public enum RegistrationLifecycleState
{
    Disabled,
    ActiveFresh,
    ActiveStale,
    ActiveUnavailable
}

public sealed record SqlProductVersion(int Major, int Minor, int Build, int Revision)
{
    public static bool TryParse(string? value, out SqlProductVersion version)
    {
        version = new(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(value)) return false;
        var pieces = value.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pieces.Length is < 2 or > 4) return false;
        var numbers = new int[4];
        for (var index = 0; index < pieces.Length; index++)
        {
            if (!int.TryParse(pieces[index], out numbers[index]) || numbers[index] < 0) return false;
        }
        version = new(numbers[0], numbers[1], numbers[2], numbers[3]);
        return version.Major > 0;
    }
}

public sealed record EstateLifecyclePolicy(int MinimumMajorVersion = 16)
{
    public void Validate()
    {
        if (MinimumMajorVersion is < 10 or > 99) throw new ArgumentOutOfRangeException(nameof(MinimumMajorVersion));
    }
}

public sealed record EstateInventoryRow(
    Guid RegistrationId,
    ServerEnvironmentClass Environment,
    SqlProductVersion? Version,
    SqlMajorGeneration Generation,
    SqlEditionClass Edition,
    SqlInstanceTopology Topology,
    EncryptionPosture Encryption,
    RegistrationLifecycleState Lifecycle,
    bool UpgradeCandidate);

public sealed record EnvironmentVersionInventory(ServerEnvironmentClass Environment, SqlMajorGeneration Generation, int Servers);

public static class EstateInventory
{
    public static SqlMajorGeneration Generation(SqlProductVersion? version) => version?.Major switch
    {
        null => SqlMajorGeneration.Unknown,
        < 15 => SqlMajorGeneration.Pre15,
        15 => SqlMajorGeneration.Major15,
        16 => SqlMajorGeneration.Major16,
        >= 17 => SqlMajorGeneration.Major17Plus
    };

    public static SqlEditionClass Edition(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return SqlEditionClass.Unknown;
        var normalized = value.Trim();
        if (normalized.Contains("Enterprise", StringComparison.OrdinalIgnoreCase)) return SqlEditionClass.Enterprise;
        if (normalized.Contains("Standard", StringComparison.OrdinalIgnoreCase)) return SqlEditionClass.Standard;
        if (normalized.Contains("Express", StringComparison.OrdinalIgnoreCase)) return SqlEditionClass.Express;
        if (normalized.Contains("Developer", StringComparison.OrdinalIgnoreCase)) return SqlEditionClass.Developer;
        return SqlEditionClass.Other;
    }

    public static SqlInstanceTopology Topology(ServerRegistration registration) =>
        string.IsNullOrWhiteSpace(registration.Endpoint.InstanceName) ? SqlInstanceTopology.DefaultInstance : SqlInstanceTopology.NamedInstance;

    public static EncryptionPosture Encryption(ServerRegistration registration) => registration.Endpoint.Encrypt switch
    {
        false => EncryptionPosture.NotEncrypted,
        true when registration.Endpoint.TrustServerCertificate => EncryptionPosture.EncryptedTrustsServerCertificate,
        _ => EncryptionPosture.Strong
    };

    public static RegistrationLifecycleState Lifecycle(ServerRegistration registration, SnapshotCacheResult? cached) =>
        !registration.IsEnabled ? RegistrationLifecycleState.Disabled : cached switch
        {
            null => RegistrationLifecycleState.ActiveUnavailable,
            { Freshness: SnapshotFreshness.Stale } => RegistrationLifecycleState.ActiveStale,
            _ => RegistrationLifecycleState.ActiveFresh
        };

    public static EstateInventoryRow Project(
        ServerRegistration registration,
        SnapshotCacheResult? cached,
        ServerOperatorMetadata metadata,
        EstateLifecyclePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        SqlProductVersion? version = null;
        if (cached is not null && SqlProductVersion.TryParse(cached.Snapshot.ProductVersion, out var parsed)) version = parsed;
        var lifecycle = Lifecycle(registration, cached);
        return new(
            registration.Id,
            metadata.Environment,
            version,
            Generation(version),
            Edition(cached?.Snapshot.Edition),
            Topology(registration),
            Encryption(registration),
            lifecycle,
            registration.IsEnabled && version is not null && version.Major < policy.MinimumMajorVersion);
    }

    public static IReadOnlyList<EnvironmentVersionInventory> Matrix(IEnumerable<EstateInventoryRow> rows) =>
        rows.GroupBy(item => (item.Environment, item.Generation))
            .OrderBy(group => group.Key.Environment)
            .ThenBy(group => group.Key.Generation)
            .Select(group => new EnvironmentVersionInventory(group.Key.Environment, group.Key.Generation, group.Count()))
            .ToArray();
}

public sealed class EstateInventoryService(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    IOperatorMetadataStore metadata,
    EstateLifecyclePolicy? policy = null)
{
    private readonly EstateLifecyclePolicy _policy = Validate(policy ?? new EstateLifecyclePolicy());

    public IReadOnlyList<EstateInventoryRow> Read() => registrations.GetAll()
        .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(item => item.Id)
        .Select(item => EstateInventory.Project(item, cache.Peek(item.Id), metadata.GetServer(item.Id), _policy))
        .ToArray();

    public IReadOnlyList<EstateInventoryRow> UpgradeCandidates() => Read().Where(item => item.UpgradeCandidate).ToArray();

    private static EstateLifecyclePolicy Validate(EstateLifecyclePolicy policy) { policy.Validate(); return policy; }
}
