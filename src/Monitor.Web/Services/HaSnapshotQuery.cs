using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed record SqlHaReplicaRow(
    string GroupKey,
    string ReplicaKey,
    bool? IsLocal,
    string AvailabilityMode,
    string FailoverMode,
    string? Role,
    string? ConnectedState,
    string? OperationalState,
    string? SynchronizationHealth);

internal sealed record SqlHaDatabaseReplicaRow(
    string GroupKey,
    string ReplicaKey,
    string DatabaseKey,
    bool? IsLocal,
    bool? IsPrimary,
    string? SynchronizationState,
    string? SynchronizationHealth,
    bool? IsSuspended,
    string? SuspendReason,
    long? LogSendQueueKb,
    long? RedoQueueKb,
    long? SecondaryLagSeconds);

internal sealed record SqlHaRow(
    bool IsHadrEnabled,
    int TotalReplicas,
    int TotalDatabaseReplicas,
    IReadOnlyList<SqlHaReplicaRow>? Replicas = null,
    IReadOnlyList<SqlHaDatabaseReplicaRow>? DatabaseReplicas = null);

internal static class HaEvidenceMapper
{
    public static HaHealthSnapshot Map(SqlHaRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var replicas = row.Replicas ?? [];
        var databases = row.DatabaseReplicas ?? [];
        var expectedReplicas = Math.Min(row.TotalReplicas, HaSnapshotQuery.MaxReplicas);
        var expectedDatabases = Math.Min(row.TotalDatabaseReplicas, HaSnapshotQuery.MaxDatabaseReplicas);

        if (row.TotalReplicas < 0 ||
            row.TotalDatabaseReplicas < 0 ||
            replicas.Count != expectedReplicas ||
            databases.Count != expectedDatabases ||
            replicas.Any(IsInvalid) ||
            databases.Any(IsInvalid) ||
            replicas.Select(replica => (replica.GroupKey, replica.ReplicaKey)).Distinct().Count() != replicas.Count)
        {
            throw new InvalidDataException("Invalid HA evidence row.");
        }

        return new HaHealthSnapshot(
            row.IsHadrEnabled,
            row.TotalReplicas,
            row.TotalDatabaseReplicas,
            replicas.Select(replica => new HaReplicaSnapshot(
                replica.GroupKey,
                replica.ReplicaKey,
                replica.IsLocal,
                replica.AvailabilityMode,
                replica.FailoverMode,
                replica.Role,
                replica.ConnectedState,
                replica.OperationalState,
                replica.SynchronizationHealth)).ToArray(),
            databases.Select(database => new HaDatabaseReplicaSnapshot(
                database.GroupKey,
                database.ReplicaKey,
                database.DatabaseKey,
                database.IsLocal,
                database.IsPrimary,
                database.SynchronizationState,
                database.SynchronizationHealth,
                database.IsSuspended,
                database.SuspendReason,
                database.LogSendQueueKb,
                database.RedoQueueKb,
                database.SecondaryLagSeconds)).ToArray(),
            row.TotalReplicas > HaSnapshotQuery.MaxReplicas,
            row.TotalDatabaseReplicas > HaSnapshotQuery.MaxDatabaseReplicas);
    }

    private static bool IsInvalid(SqlHaReplicaRow replica) =>
        string.IsNullOrWhiteSpace(replica.GroupKey) ||
        replica.GroupKey.Length > 128 ||
        string.IsNullOrWhiteSpace(replica.ReplicaKey) ||
        replica.ReplicaKey.Length > 256 ||
        string.IsNullOrWhiteSpace(replica.AvailabilityMode) ||
        replica.AvailabilityMode.Length > 60 ||
        string.IsNullOrWhiteSpace(replica.FailoverMode) ||
        replica.FailoverMode.Length > 60 ||
        TooLong(replica.Role) ||
        TooLong(replica.ConnectedState) ||
        TooLong(replica.OperationalState) ||
        TooLong(replica.SynchronizationHealth);

    private static bool IsInvalid(SqlHaDatabaseReplicaRow database) =>
        string.IsNullOrWhiteSpace(database.GroupKey) ||
        database.GroupKey.Length > 128 ||
        string.IsNullOrWhiteSpace(database.ReplicaKey) ||
        database.ReplicaKey.Length > 256 ||
        string.IsNullOrWhiteSpace(database.DatabaseKey) ||
        database.DatabaseKey.Length > 128 ||
        TooLong(database.SynchronizationState) ||
        TooLong(database.SynchronizationHealth) ||
        TooLong(database.SuspendReason) ||
        database.LogSendQueueKb is < 0 ||
        database.RedoQueueKb is < 0 ||
        database.SecondaryLagSeconds is < 0;

    private static bool TooLong(string? value) => value?.Length > 60;
}

internal sealed class HaSnapshotQuery(PerformanceScaleOptions? performance = null)
{
    internal const int MaxReplicas = 16;
    internal const int MaxDatabaseReplicas = 64;

    internal const string CommandText = """
        SELECT
            CONVERT(bit, COALESCE(SERVERPROPERTY(N'IsHadrEnabled'), 0)) AS IsHadrEnabled,
            (SELECT COUNT(*) FROM sys.availability_replicas) AS TotalReplicas,
            (SELECT COUNT(*) FROM sys.dm_hadr_database_replica_states WHERE database_id > 4) AS TotalDatabaseReplicas,
            (SELECT TOP (16)
                  CONVERT(nvarchar(128), ag.name) AS GroupKey,
                  CONVERT(nvarchar(256), ar.replica_server_name) AS ReplicaKey,
                  ars.is_local AS IsLocal,
                  CONVERT(nvarchar(60), ar.availability_mode_desc) AS AvailabilityMode,
                  CONVERT(nvarchar(60), ar.failover_mode_desc) AS FailoverMode,
                  CONVERT(nvarchar(60), ars.role_desc) AS Role,
                  CONVERT(nvarchar(60), ars.connected_state_desc) AS ConnectedState,
                  CONVERT(nvarchar(60), ars.operational_state_desc) AS OperationalState,
                  CONVERT(nvarchar(60), ars.synchronization_health_desc) AS SynchronizationHealth
              FROM sys.availability_replicas AS ar
              INNER JOIN sys.availability_groups AS ag ON ag.group_id = ar.group_id
              LEFT JOIN sys.dm_hadr_availability_replica_states AS ars
                ON ars.group_id = ar.group_id AND ars.replica_id = ar.replica_id
              ORDER BY
                  CASE WHEN ars.connected_state_desc = N'DISCONNECTED' THEN 0
                       WHEN ars.synchronization_health_desc = N'NOT_HEALTHY' THEN 1
                       WHEN ars.synchronization_health_desc = N'PARTIALLY_HEALTHY' THEN 2
                       WHEN ars.connected_state_desc IS NULL THEN 3 ELSE 4 END,
                  ag.name,
                  ar.replica_server_name
              FOR JSON PATH) AS ReplicasJson,
            (SELECT TOP (64)
                  CONVERT(nvarchar(128), ag.name) AS GroupKey,
                  CONVERT(nvarchar(256), ar.replica_server_name) AS ReplicaKey,
                  CONVERT(nvarchar(128), d.name) AS DatabaseKey,
                  drs.is_local AS IsLocal,
                  drs.is_primary_replica AS IsPrimary,
                  CONVERT(nvarchar(60), drs.synchronization_state_desc) AS SynchronizationState,
                  CONVERT(nvarchar(60), drs.synchronization_health_desc) AS SynchronizationHealth,
                  drs.is_suspended AS IsSuspended,
                  CONVERT(nvarchar(60), drs.suspend_reason_desc) AS SuspendReason,
                  CONVERT(bigint, drs.log_send_queue_size) AS LogSendQueueKb,
                  CONVERT(bigint, drs.redo_queue_size) AS RedoQueueKb,
                  CONVERT(bigint, drs.secondary_lag_seconds) AS SecondaryLagSeconds
              FROM sys.dm_hadr_database_replica_states AS drs
              INNER JOIN sys.availability_replicas AS ar
                ON ar.group_id = drs.group_id AND ar.replica_id = drs.replica_id
              INNER JOIN sys.availability_groups AS ag ON ag.group_id = drs.group_id
              INNER JOIN sys.databases AS d ON d.database_id = drs.database_id
              WHERE d.database_id > 4
              ORDER BY
                  CASE WHEN drs.is_suspended = 1 THEN 0
                       WHEN drs.synchronization_health_desc = N'NOT_HEALTHY' THEN 1
                       WHEN drs.synchronization_health_desc = N'PARTIALLY_HEALTHY' THEN 2 ELSE 3 END,
                  COALESCE(drs.secondary_lag_seconds, -1) DESC,
                  CASE WHEN COALESCE(drs.log_send_queue_size, 0) >= COALESCE(drs.redo_queue_size, 0)
                       THEN COALESCE(drs.log_send_queue_size, 0) ELSE COALESCE(drs.redo_queue_size, 0) END DESC,
                  ag.name,
                  d.name,
                  ar.replica_server_name
              FOR JSON PATH) AS DatabaseReplicasJson;
        """;

    public async Task<SqlHaRow> ExecuteAsync(
        ServerRegistration registration,
        SqlLoginSecret? secret,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = SqlConnectionStringFactory.Create(
                registration,
                secret,
                "Monitor/HaEvidence",
                performance);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = CommandText;
            command.CommandTimeout = 2;
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidDataException("HA evidence query returned no row.");

            return new SqlHaRow(
                reader.GetBoolean(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                ReadRows<SqlHaReplicaRow>(reader, 3),
                ReadRows<SqlHaDatabaseReplicaRow>(reader, 4));
        }
        catch (SqlException exception)
        {
            throw new SqlProbeException(SqlErrorClassifier.Classify(exception.Number));
        }
    }

    private static IReadOnlyList<T> ReadRows<T>(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return [];
        var json = reader.GetString(ordinal);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<T[]>(json) ?? [];
    }
}
