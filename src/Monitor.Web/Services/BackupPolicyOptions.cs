namespace Monitor.Web.Services;

public sealed class BackupPolicyOptions
{
    public const string SectionName = "BackupPolicy";

    public bool Enabled { get; init; }
    public int? FullRpoMinutes { get; init; }
    public int? LogRpoMinutes { get; init; }

    public bool IsConfigured => Enabled && FullRpoMinutes.HasValue && LogRpoMinutes.HasValue;

    public TimeSpan? FullRpo => IsConfigured ? TimeSpan.FromMinutes(FullRpoMinutes!.Value) : null;
    public TimeSpan? LogRpo => IsConfigured ? TimeSpan.FromMinutes(LogRpoMinutes!.Value) : null;

    public void Validate()
    {
        ValidatePositive(nameof(FullRpoMinutes), FullRpoMinutes);
        ValidatePositive(nameof(LogRpoMinutes), LogRpoMinutes);

        if (Enabled && (!FullRpoMinutes.HasValue || !LogRpoMinutes.HasValue))
        {
            throw new InvalidOperationException(
                "BackupPolicy is enabled but FullRpoMinutes and LogRpoMinutes are not both configured. " +
                "Monitor does not invent backup RPO values.");
        }
    }

    private static void ValidatePositive(string name, int? value)
    {
        if (value.HasValue && value.Value <= 0)
            throw new InvalidOperationException($"BackupPolicy:{name} must be greater than zero when configured.");
    }
}
