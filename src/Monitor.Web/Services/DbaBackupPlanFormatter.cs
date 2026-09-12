using System.Globalization;
using System.Text;

namespace Monitor.Web.Services;

internal static class DbaBackupPlanFormatter
{
    public static string Build(DbaCommandCenterViewModel model, Guid registrationId, string? databaseName)
    {
        var server = model.Servers.SingleOrDefault(item => item.RegistrationId == registrationId)
            ?? throw new KeyNotFoundException("Server registration was not found.");

        var databases = server.Diagnostics?.Databases
            .Where(database => string.IsNullOrWhiteSpace(databaseName) || string.Equals(database.DatabaseName, databaseName, StringComparison.OrdinalIgnoreCase))
            .ToArray() ?? [];

        if (!string.IsNullOrWhiteSpace(databaseName) && databases.Length == 0)
            throw new KeyNotFoundException("The requested database is not present in the latest DBA inspection.");

        var builder = new StringBuilder();
        builder.AppendLine("/* MONITOR DBA BACKUP MANAGEMENT PLAN");
        builder.AppendLine($"   Server: {server.DisplayName}");
        builder.AppendLine($"   Scope: {(string.IsNullOrWhiteSpace(databaseName) ? "all inspected user databases" : databaseName)}");
        builder.AppendLine("   SAFETY: REVIEW ONLY. Validate paths, retention, encryption, credentials and restore procedures before scheduling.");
        builder.AppendLine("   Recommended cadence: FULL daily 23:00; DIFFERENTIAL every 6 hours; LOG every 15 minutes for FULL/BULK_LOGGED; CHECKDB weekly; restore test weekly.");
        builder.AppendLine("*/");
        builder.AppendLine("DECLARE @BackupRoot nvarchar(4000) = N'<BACKUP_ROOT>'; -- CHANGE THIS");
        builder.AppendLine("DECLARE @FullPath nvarchar(4000);");
        builder.AppendLine();

        foreach (var database in databases)
        {
            var quoted = QuoteIdentifier(database.DatabaseName);
            var literal = EscapeLiteral(database.DatabaseName);
            builder.AppendLine($"-- {database.DatabaseName} | Recovery={database.RecoveryModel} | Last full={(database.LastFullBackupAtUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "not observed")}");
            builder.AppendLine($"SET @FullPath = @BackupRoot + N'\\{literal}_FULL_' + CONVERT(nvarchar(8), GETDATE(), 112) + N'.bak';");
            builder.AppendLine($"BACKUP DATABASE {quoted} TO DISK = @FullPath WITH CHECKSUM, COMPRESSION, STATS = 10;");
            builder.AppendLine("RESTORE VERIFYONLY FROM DISK = @FullPath WITH CHECKSUM;");
            builder.AppendLine($"-- Differential job template: BACKUP DATABASE {quoted} TO DISK = N'<DIFF_FILE>' WITH DIFFERENTIAL, CHECKSUM, COMPRESSION, STATS = 10;");
            if (database.RecoveryModel is "FULL" or "BULK_LOGGED")
                builder.AppendLine($"-- Log job template (15 min): BACKUP LOG {quoted} TO DISK = N'<LOG_FILE>' WITH CHECKSUM, COMPRESSION, STATS = 10;");
            builder.AppendLine($"-- Weekly integrity template: DBCC CHECKDB (N'{literal}') WITH NO_INFOMSGS, ALL_ERRORMSGS;");
            builder.AppendLine();
        }

        if (databases.Length == 0)
            builder.AppendLine("-- Run a DBA inspection first so Monitor can enumerate the intended user-database scope safely.");

        return builder.ToString();
    }

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
    private static string EscapeLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
