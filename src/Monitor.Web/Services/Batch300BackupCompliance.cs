namespace Monitor.Web.Services;

public enum BackupRisk
{
    Unknown,
    Compliant,
    Warning,
    Critical
}

public enum RecoveryModelClass
{
    Unknown,
    Simple,
    Full,
    BulkLogged
}

public sealed record BackupComplianceInput(
    string? RecoveryModel,
    DateTimeOffset? LastFullUtc,
    DateTimeOffset? LastLogUtc,
    DateTimeOffset NowUtc,
    TimeSpan FullRpo,
    TimeSpan LogRpo,
    bool IsSystemDatabase = false);

public sealed record BackupComplianceResult(BackupRisk Risk, int Score, bool FullOverdue, bool LogOverdue, string[] Reasons);

public static class Batch300BackupCompliance
{
    public static RecoveryModelClass ClassifyRecoveryModel(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "SIMPLE" => RecoveryModelClass.Simple,
        "FULL" => RecoveryModelClass.Full,
        "BULK_LOGGED" or "BULK LOGGED" => RecoveryModelClass.BulkLogged,
        _ => RecoveryModelClass.Unknown
    };

    public static TimeSpan? Age(DateTimeOffset nowUtc, DateTimeOffset? backupUtc)
    {
        if (backupUtc is null) return null;
        var age = nowUtc - backupUtc.Value;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    public static bool IsFullOverdue(BackupComplianceInput input)
    {
        if (input.FullRpo <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(input));
        var age = Age(input.NowUtc, input.LastFullUtc);
        return age is null || age > input.FullRpo;
    }

    public static bool RequiresLogBackup(BackupComplianceInput input)
    {
        var model = ClassifyRecoveryModel(input.RecoveryModel);
        return !input.IsSystemDatabase && model is RecoveryModelClass.Full or RecoveryModelClass.BulkLogged;
    }

    public static bool IsLogOverdue(BackupComplianceInput input)
    {
        if (!RequiresLogBackup(input)) return false;
        if (input.LogRpo <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(input));
        var age = Age(input.NowUtc, input.LastLogUtc);
        return age is null || age > input.LogRpo;
    }

    public static int Score(BackupComplianceInput input)
    {
        var score = 100;
        if (IsFullOverdue(input)) score -= 60;
        if (IsLogOverdue(input)) score -= 35;
        if (ClassifyRecoveryModel(input.RecoveryModel) == RecoveryModelClass.Unknown) score -= 10;
        return Math.Clamp(score, 0, 100);
    }

    public static BackupRisk ClassifyRisk(int score) => score switch
    {
        >= 90 => BackupRisk.Compliant,
        >= 60 => BackupRisk.Warning,
        >= 0 => BackupRisk.Critical,
        _ => BackupRisk.Unknown
    };

    public static string[] Reasons(BackupComplianceInput input)
    {
        var reasons = new List<string>();
        if (IsFullOverdue(input)) reasons.Add("Full backup is outside the configured RPO.");
        if (IsLogOverdue(input)) reasons.Add("Log backup is outside the configured RPO.");
        if (ClassifyRecoveryModel(input.RecoveryModel) == RecoveryModelClass.Unknown) reasons.Add("Recovery model is unknown.");
        return reasons.Count == 0 ? ["Backup policy is within the configured RPO."] : reasons.ToArray();
    }

    public static string ComplianceLabel(BackupComplianceInput input) => ClassifyRisk(Score(input)).ToString();

    public static BackupComplianceResult Evaluate(BackupComplianceInput input)
    {
        var score = Score(input);
        return new(ClassifyRisk(score), score, IsFullOverdue(input), IsLogOverdue(input), Reasons(input));
    }
}
