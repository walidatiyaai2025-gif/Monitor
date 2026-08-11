using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Monitor.Web.Services;

public sealed record OperatorStateBackupEnvelope(
    string Schema,
    long SourceVersion,
    DateTimeOffset CapturedAtUtc,
    string PayloadSha256,
    string PayloadJson);

public sealed record OperatorStateRestoreValidation(bool Valid, string Message, long SourceVersion, string PayloadSha256);
public sealed record OperatorStateRestoreResult(bool Applied, bool RolledBack, string Message, long? Version);
public sealed record OperatorStateHaDiagnostics(string Status, long? Version, string Message);

public interface IOperatorStateRecoveryService
{
    Task<byte[]> ExportAsync(CancellationToken cancellationToken = default);
    OperatorStateRestoreValidation DryRun(ReadOnlySpan<byte> package);
    Task<OperatorStateRestoreResult> RestoreAsync(ReadOnlyMemory<byte> package, CancellationToken cancellationToken = default);
    Task<OperatorStateHaDiagnostics> DiagnosticsAsync(CancellationToken cancellationToken = default);
}

public sealed class OperatorStateRecoveryService(
    ISharedStateDocumentStore shared,
    TimeProvider timeProvider) : IOperatorStateRecoveryService
{
    public const string StateKey = "monitor:operator-metadata:v1";
    public const string BackupSchema = "monitor-operator-shared-backup-v1";
    private const int MaxPayloadBytes = 2 * 1024 * 1024;

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        var document = await shared.ReadAsync(StateKey, cancellationToken)
            ?? throw new InvalidOperationException("Shared operator state is not initialized.");
        ValidatePayload(document.PayloadJson);
        var envelope = new OperatorStateBackupEnvelope(
            BackupSchema,
            document.Version,
            timeProvider.GetUtcNow(),
            Sha256(document.PayloadJson),
            document.PayloadJson);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
    }

    public OperatorStateRestoreValidation DryRun(ReadOnlySpan<byte> package)
    {
        try
        {
            var envelope = Parse(package);
            ValidatePayload(envelope.PayloadJson);
            var digest = Sha256(envelope.PayloadJson);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(digest), Convert.FromHexString(envelope.PayloadSha256)))
                return new(false, "Operator-state backup checksum does not match.", envelope.SourceVersion, digest);
            return new(true, "Operator-state backup is valid for shared-state restore.", envelope.SourceVersion, digest);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException or FormatException)
        {
            return new(false, "Operator-state backup is invalid or corrupt.", 0, string.Empty);
        }
    }

    public async Task<OperatorStateRestoreResult> RestoreAsync(ReadOnlyMemory<byte> package, CancellationToken cancellationToken = default)
    {
        var validation = DryRun(package.Span);
        if (!validation.Valid) return new(false, false, validation.Message, null);
        var envelope = Parse(package.Span);
        var before = await shared.ReadAsync(StateKey, cancellationToken);
        if (before is null) return new(false, false, "Shared operator state is not initialized.", null);

        var applied = await shared.CompareExchangeAsync(StateKey, before.Version, envelope.PayloadJson, cancellationToken);
        if (applied.Status != SharedStateWriteStatus.Applied || applied.Document is null)
            return new(false, false, "Shared operator state changed before restore could be committed.", before.Version);

        try
        {
            var verify = await shared.ReadAsync(StateKey, cancellationToken)
                ?? throw new InvalidDataException("Restored shared operator state could not be read back.");
            if (!string.Equals(Sha256(verify.PayloadJson), envelope.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Restored shared operator state failed checksum verification.");
            return new(true, false, "Shared operator state restored and verified.", verify.Version);
        }
        catch (Exception exception) when (exception is InvalidDataException or SharedStateStoreUnavailableException)
        {
            var current = applied.Document;
            var rollback = await shared.CompareExchangeAsync(StateKey, current.Version, before.PayloadJson, CancellationToken.None);
            var rolledBack = rollback.Status == SharedStateWriteStatus.Applied;
            return new(false, rolledBack, rolledBack ? "Restore verification failed; previous shared operator state was restored." : "Restore verification failed and rollback could not be confirmed.", rollback.Document?.Version);
        }
    }

    public async Task<OperatorStateHaDiagnostics> DiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await shared.ReadAsync(StateKey, cancellationToken);
            return document is null
                ? new("uninitialized", null, "Shared operator state has no document.")
                : new("ready", document.Version, "Shared operator state is readable.");
        }
        catch (SharedStateStoreUnavailableException)
        {
            return new("unavailable", null, "Shared operator state is temporarily unavailable.");
        }
    }

    private static OperatorStateBackupEnvelope Parse(ReadOnlySpan<byte> package)
    {
        if (package.Length == 0 || package.Length > MaxPayloadBytes) throw new InvalidDataException("Operator-state backup size is invalid.");
        var envelope = JsonSerializer.Deserialize<OperatorStateBackupEnvelope>(package, JsonOptions)
            ?? throw new InvalidDataException("Operator-state backup envelope is missing.");
        if (!string.Equals(envelope.Schema, BackupSchema, StringComparison.Ordinal) || envelope.SourceVersion <= 0 || envelope.CapturedAtUtc == default)
            throw new InvalidDataException("Operator-state backup envelope metadata is invalid.");
        if (envelope.PayloadSha256.Length != 64) throw new InvalidDataException("Operator-state backup checksum is invalid.");
        return envelope;
    }

    private static void ValidatePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) || Encoding.UTF8.GetByteCount(payloadJson) > MaxPayloadBytes) throw new InvalidDataException("Shared operator-state payload is invalid or too large.");
        using var document = JsonDocument.Parse(payloadJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Shared operator-state payload must be a JSON object.");
    }

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
