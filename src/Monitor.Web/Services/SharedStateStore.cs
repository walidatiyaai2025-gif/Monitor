using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum SharedStateProviderKind
{
    Disabled,
    SqlServer
}

public sealed class SharedStateOptions
{
    public const string SectionName = "SharedState";
    private const int MaximumEnvironmentVariableNameLength = 128;

    public SharedStateProviderKind Provider { get; set; } = SharedStateProviderKind.Disabled;
    public string ConnectionStringEnvironmentVariable { get; set; } = "MONITOR_SHARED_STATE_SQL_CONNECTION";
    public int CommandTimeoutSeconds { get; set; } = 5;

    public void Validate()
    {
        if (!Enum.IsDefined(Provider))
        {
            throw new InvalidOperationException("SharedState:Provider is not supported.");
        }

        if (CommandTimeoutSeconds is < 1 or > 30)
        {
            throw new InvalidOperationException("SharedState:CommandTimeoutSeconds must be between 1 and 30.");
        }

        if (Provider == SharedStateProviderKind.Disabled)
        {
            return;
        }

        if (!IsSafeEnvironmentVariableName(ConnectionStringEnvironmentVariable))
        {
            throw new InvalidOperationException("SharedState:ConnectionStringEnvironmentVariable is invalid.");
        }
    }

    private static bool IsSafeEnvironmentVariableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumEnvironmentVariableNameLength)
        {
            return false;
        }

        if (!char.IsAsciiLetter(value[0]) && value[0] != '_')
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record SharedStateDocument(
    string Key,
    long Version,
    string PayloadJson,
    DateTimeOffset UpdatedAtUtc);

public enum SharedStateWriteStatus
{
    Applied,
    Conflict
}

public sealed record SharedStateWriteResult(
    SharedStateWriteStatus Status,
    SharedStateDocument? Document)
{
    public bool Applied => Status == SharedStateWriteStatus.Applied;
}

public interface ISharedStateDocumentStore
{
    Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default);

    Task<SharedStateWriteResult> CompareExchangeAsync(
        string key,
        long expectedVersion,
        string payloadJson,
        CancellationToken cancellationToken = default);
}

public sealed class SharedStateStoreUnavailableException : Exception
{
    public SharedStateStoreUnavailableException()
        : base("Shared state provider is unavailable.")
    {
    }
}

internal interface ISharedStateSqlBackend
{
    Task<int?> ReadSchemaVersionAsync(
        string connectionString,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);

    Task<SharedStateDocument?> ReadAsync(
        string connectionString,
        string key,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);

    Task<SharedStateWriteResult> CompareExchangeAsync(
        string connectionString,
        string key,
        long expectedVersion,
        string payloadJson,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);
}

internal sealed class SqlServerSharedStateSqlBackend : ISharedStateSqlBackend
{
    internal const int MaximumTransportPayloadBytes = SqlServerSharedStateDocumentStore.MaximumPayloadBytes * 2;

    private const string SchemaVersionSql = """
        SET NOCOUNT ON;
        IF OBJECT_ID(N'dbo.MonitorSharedStateSchema', N'U') IS NULL
        BEGIN
            SELECT CAST(NULL AS int) AS SchemaVersion;
            RETURN;
        END;

        SELECT TOP (1) SchemaVersion
        FROM dbo.MonitorSharedStateSchema
        WHERE Id = 1;
        """;

    private const string ReadSql = """
        SET NOCOUNT ON;
        SELECT
            DocumentKey,
            Version,
            CASE
                WHEN DATALENGTH(PayloadJson) <= @MaximumTransportPayloadBytes THEN PayloadJson
                ELSE NULL
            END AS PayloadJson,
            DATALENGTH(PayloadJson) AS PayloadStorageBytes,
            UpdatedAtUtc
        FROM dbo.MonitorSharedStateDocuments
        WHERE DocumentKey = @DocumentKey;
        """;

    private const string CompareExchangeSql = """
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        BEGIN TRANSACTION;

        DECLARE @CurrentVersion bigint = NULL;
        DECLARE @Result TABLE
        (
            Applied bit NOT NULL,
            Version bigint NULL,
            PayloadJson nvarchar(max) NULL,
            PayloadStorageBytes bigint NULL,
            UpdatedAtUtc datetime2(7) NULL
        );

        SELECT @CurrentVersion = Version
        FROM dbo.MonitorSharedStateDocuments WITH (UPDLOCK, HOLDLOCK)
        WHERE DocumentKey = @DocumentKey;

        IF @CurrentVersion IS NULL
        BEGIN
            IF @ExpectedVersion <> 0
            BEGIN
                INSERT @Result (Applied, Version, PayloadJson, PayloadStorageBytes, UpdatedAtUtc)
                VALUES (0, NULL, NULL, NULL, NULL);
            END
            ELSE
            BEGIN
                INSERT dbo.MonitorSharedStateDocuments (DocumentKey, Version, PayloadJson, UpdatedAtUtc)
                VALUES (@DocumentKey, 1, @PayloadJson, SYSUTCDATETIME());

                INSERT @Result (Applied, Version, PayloadJson, PayloadStorageBytes, UpdatedAtUtc)
                SELECT
                    1,
                    Version,
                    CASE
                        WHEN DATALENGTH(PayloadJson) <= @MaximumTransportPayloadBytes THEN PayloadJson
                        ELSE NULL
                    END,
                    DATALENGTH(PayloadJson),
                    UpdatedAtUtc
                FROM dbo.MonitorSharedStateDocuments
                WHERE DocumentKey = @DocumentKey;
            END;
        END
        ELSE IF @CurrentVersion <> @ExpectedVersion
        BEGIN
            INSERT @Result (Applied, Version, PayloadJson, PayloadStorageBytes, UpdatedAtUtc)
            SELECT
                0,
                Version,
                CASE
                    WHEN DATALENGTH(PayloadJson) <= @MaximumTransportPayloadBytes THEN PayloadJson
                    ELSE NULL
                END,
                DATALENGTH(PayloadJson),
                UpdatedAtUtc
            FROM dbo.MonitorSharedStateDocuments
            WHERE DocumentKey = @DocumentKey;
        END
        ELSE
        BEGIN
            UPDATE dbo.MonitorSharedStateDocuments
            SET Version = Version + 1,
                PayloadJson = @PayloadJson,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE DocumentKey = @DocumentKey;

            INSERT @Result (Applied, Version, PayloadJson, PayloadStorageBytes, UpdatedAtUtc)
            SELECT
                1,
                Version,
                CASE
                    WHEN DATALENGTH(PayloadJson) <= @MaximumTransportPayloadBytes THEN PayloadJson
                    ELSE NULL
                END,
                DATALENGTH(PayloadJson),
                UpdatedAtUtc
            FROM dbo.MonitorSharedStateDocuments
            WHERE DocumentKey = @DocumentKey;
        END;

        COMMIT TRANSACTION;

        SELECT Applied, Version, PayloadJson, PayloadStorageBytes, UpdatedAtUtc
        FROM @Result;
        """;

    public async Task<int?> ReadSchemaVersionAsync(
        string connectionString,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SchemaVersionSql, connection)
        {
            CommandTimeout = commandTimeoutSeconds
        };
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull
            ? null
            : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<SharedStateDocument?> ReadAsync(
        string connectionString,
        string key,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(ReadSql, connection)
        {
            CommandTimeout = commandTimeoutSeconds
        };
        command.Parameters.Add(new SqlParameter("@DocumentKey", SqlDbType.NVarChar, 128) { Value = key });
        command.Parameters.Add(new SqlParameter("@MaximumTransportPayloadBytes", SqlDbType.BigInt) { Value = MaximumTransportPayloadBytes });

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadDocument(
            key,
            reader,
            versionOrdinal: 1,
            payloadOrdinal: 2,
            payloadStorageBytesOrdinal: 3,
            updatedOrdinal: 4);
    }

    public async Task<SharedStateWriteResult> CompareExchangeAsync(
        string connectionString,
        string key,
        long expectedVersion,
        string payloadJson,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(CompareExchangeSql, connection)
        {
            CommandTimeout = commandTimeoutSeconds
        };
        command.Parameters.Add(new SqlParameter("@DocumentKey", SqlDbType.NVarChar, 128) { Value = key });
        command.Parameters.Add(new SqlParameter("@ExpectedVersion", SqlDbType.BigInt) { Value = expectedVersion });
        command.Parameters.Add(new SqlParameter("@PayloadJson", SqlDbType.NVarChar, -1) { Value = payloadJson });
        command.Parameters.Add(new SqlParameter("@MaximumTransportPayloadBytes", SqlDbType.BigInt) { Value = MaximumTransportPayloadBytes });

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Shared-state compare/exchange returned no result.");
        }

        var applied = reader.GetBoolean(0);
        if (reader.IsDBNull(1))
        {
            return new SharedStateWriteResult(
                applied ? SharedStateWriteStatus.Applied : SharedStateWriteStatus.Conflict,
                null);
        }

        var document = ReadDocument(
            key,
            reader,
            versionOrdinal: 1,
            payloadOrdinal: 2,
            payloadStorageBytesOrdinal: 3,
            updatedOrdinal: 4);
        return new SharedStateWriteResult(
            applied ? SharedStateWriteStatus.Applied : SharedStateWriteStatus.Conflict,
            document);
    }

    private static SharedStateDocument ReadDocument(
        string key,
        SqlDataReader reader,
        int versionOrdinal,
        int payloadOrdinal,
        int payloadStorageBytesOrdinal,
        int updatedOrdinal)
    {
        if (reader.IsDBNull(payloadStorageBytesOrdinal))
        {
            throw new InvalidDataException("Shared-state payload length is unavailable.");
        }

        var payloadStorageBytes = reader.GetInt64(payloadStorageBytesOrdinal);
        if (payloadStorageBytes < 0 ||
            payloadStorageBytes > MaximumTransportPayloadBytes ||
            reader.IsDBNull(payloadOrdinal))
        {
            throw new InvalidDataException("Shared-state payload exceeds its bounded transport size.");
        }

        var updated = reader.GetDateTime(updatedOrdinal);
        return new SharedStateDocument(
            key,
            reader.GetInt64(versionOrdinal),
            reader.GetString(payloadOrdinal),
            new DateTimeOffset(DateTime.SpecifyKind(updated, DateTimeKind.Utc)));
    }
}

public sealed class SqlServerSharedStateDocumentStore : ISharedStateDocumentStore
{
    public const int SupportedSchemaVersion = 1;
    public const int MaximumKeyLength = 128;
    public const int MaximumPayloadBytes = 1_048_576;

    private readonly SharedStateOptions _options;
    private readonly ISharedStateSqlBackend _backend;
    private readonly Func<string, string?> _readEnvironmentVariable;

    public SqlServerSharedStateDocumentStore(SharedStateOptions options)
        : this(options, new SqlServerSharedStateSqlBackend(), Environment.GetEnvironmentVariable)
    {
    }

    internal SqlServerSharedStateDocumentStore(
        SharedStateOptions options,
        ISharedStateSqlBackend backend,
        Func<string, string?> readEnvironmentVariable)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _readEnvironmentVariable = readEnvironmentVariable ?? throw new ArgumentNullException(nameof(readEnvironmentVariable));
        _options.Validate();
    }

    public async Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        var connectionString = ResolveConnectionString();
        try
        {
            var document = await _backend.ReadAsync(
                connectionString,
                key,
                _options.CommandTimeoutSeconds,
                cancellationToken);
            ValidateReturnedDocument(key, document);
            return document;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            throw new SharedStateStoreUnavailableException();
        }
    }

    public async Task<SharedStateWriteResult> CompareExchangeAsync(
        string key,
        long expectedVersion,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        }
        ValidatePayload(payloadJson);

        var connectionString = ResolveConnectionString();
        try
        {
            var result = await _backend.CompareExchangeAsync(
                connectionString,
                key,
                expectedVersion,
                payloadJson,
                _options.CommandTimeoutSeconds,
                cancellationToken);
            ValidateReturnedWriteResult(key, result);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            throw new SharedStateStoreUnavailableException();
        }
    }

    internal async Task<int?> ReadSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = ResolveConnectionString();
        try
        {
            return await _backend.ReadSchemaVersionAsync(
                connectionString,
                _options.CommandTimeoutSeconds,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            throw new SharedStateStoreUnavailableException();
        }
    }

    private string ResolveConnectionString()
    {
        if (_options.Provider != SharedStateProviderKind.SqlServer)
        {
            throw new SharedStateStoreUnavailableException();
        }

        var value = _readEnvironmentVariable(_options.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SharedStateStoreUnavailableException();
        }

        return value;
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaximumKeyLength)
        {
            throw new ArgumentException("Shared-state document key is invalid.", nameof(key));
        }

        foreach (var character in key)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ':' and not '.' and not '_' and not '-')
            {
                throw new ArgumentException("Shared-state document key is invalid.", nameof(key));
            }
        }
    }

    private static void ValidatePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) ||
            System.Text.Encoding.UTF8.GetByteCount(payloadJson) > MaximumPayloadBytes)
        {
            throw new ArgumentException("Shared-state payload is invalid.", nameof(payloadJson));
        }

        try
        {
            using var _ = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Shared-state payload must be valid JSON.", nameof(payloadJson), exception);
        }
    }

    private static void ValidateReturnedWriteResult(string expectedKey, SharedStateWriteResult result)
    {
        if (!Enum.IsDefined(result.Status) || (result.Applied && result.Document is null))
        {
            throw new InvalidDataException("Shared-state provider returned an invalid compare/exchange result.");
        }

        ValidateReturnedDocument(expectedKey, result.Document);
    }

    private static void ValidateReturnedDocument(string expectedKey, SharedStateDocument? document)
    {
        if (document is null)
        {
            return;
        }

        if (!string.Equals(document.Key, expectedKey, StringComparison.Ordinal) ||
            document.Version < 1 ||
            document.UpdatedAtUtc == default ||
            string.IsNullOrWhiteSpace(document.PayloadJson) ||
            System.Text.Encoding.UTF8.GetByteCount(document.PayloadJson) > MaximumPayloadBytes)
        {
            throw new InvalidDataException("Shared-state provider returned invalid bounded document state.");
        }

        try
        {
            using var _ = JsonDocument.Parse(document.PayloadJson);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared-state provider returned invalid JSON state.", exception);
        }
    }
}

public interface ISharedStateReadinessService
{
    Task<SharedStateReadinessViewModel> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class SharedStateReadinessService : ISharedStateReadinessService
{
    private readonly SharedStateOptions _options;
    private readonly SqlServerSharedStateDocumentStore? _sqlStore;

    public SharedStateReadinessService(SharedStateOptions options, ISharedStateDocumentStore store)
    {
        _options = options;
        _sqlStore = store as SqlServerSharedStateDocumentStore;
    }

    public async Task<SharedStateReadinessViewModel> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_options.Provider == SharedStateProviderKind.Disabled)
        {
            return SharedStateReadinessViewModel.Disabled();
        }

        if (_sqlStore is null)
        {
            return SharedStateReadinessViewModel.Unavailable("Configured shared-state provider is unavailable.");
        }

        try
        {
            var schemaVersion = await _sqlStore.ReadSchemaVersionAsync(cancellationToken);
            if (schemaVersion != SqlServerSharedStateDocumentStore.SupportedSchemaVersion)
            {
                return SharedStateReadinessViewModel.SchemaMismatch(schemaVersion);
            }

            return SharedStateReadinessViewModel.Ready(schemaVersion.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return SharedStateReadinessViewModel.Unavailable(
                "Shared-state provider is configured but not currently ready.");
        }
    }
}

public sealed class DisabledSharedStateDocumentStore : ISharedStateDocumentStore
{
    public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromException<SharedStateDocument?>(new SharedStateStoreUnavailableException());

    public Task<SharedStateWriteResult> CompareExchangeAsync(
        string key,
        long expectedVersion,
        string payloadJson,
        CancellationToken cancellationToken = default) =>
        Task.FromException<SharedStateWriteResult>(new SharedStateStoreUnavailableException());
}
