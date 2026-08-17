using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed record SqlHaReplicaRow(
    string GroupKey,
    string DatabaseKey,
    string? Role,
    string? SynchronizationState,
    string? SynchronizationHealth,
    bool? Connected,
    bool AutomaticFailover,
    bool IsSuspended,
    long? SendQueueBytes,
    long? RedoQueueBytes,
    long? LagSeconds);

internal sealed record SqlHaRow(
    bool IsHadrEnabled,
    int TotalLocalDatabaseReplicas,
    IReadOnlyList<SqlHaReplicaRow>? Replicas,
    string? QuorumState,
    int? HealthyVotes,
    int? TotalVotes);

internal static class HaEvidenceMapper
{
    public static HaHealthSnapshot Map(SqlHaRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var replicas = row.Replicas ?? [];
        var expectedRows = Math.Min(row.TotalLocalDatabaseReplicas, HaSnapshotQuery.MaxReplicas);
        var quorumAllMissing = row.QuorumState is null && row.HealthyVotes is null && row.TotalVotes is null;
        var quorumComplete = !string.IsNullOrWhiteSpace(row.QuorumState) && row.HealthyVotes.HasValue && row.TotalVotes.HasValue;

        if (row.TotalLocalDatabaseReplicas < 0 ||
            replicas.Count != expectedRows ||
            (!row.IsHadrEnabled && (row.TotalLocalDatabaseReplicas != 0 || replicas.Count != 0)) ||
            (!quorumAllMissing && !quorumComplete) ||
            (quorumComplete && (row.QuorumState!.Length > 60 || row.TotalVotes <= 0 || row.HealthyVotes < 0 || row.HealthyVotes > row.TotalVotes)) ||
            replicas.Select(replica => $"{replica.GroupKey}\u001f{replica.DatabaseKey}").Distinct(StringComparer.OrdinalIgnoreCase).Count() != replicas.Count ||
            replicas.Any(replica =>
                string.IsNullOrWhiteSpace(replica.GroupKey) || replica.GroupKey.Length > 128 ||
                string.IsNullOrWhiteSpace(replica.DatabaseKey) || replica.DatabaseKey.Length > 128 ||
                replica.Role?.Length > 60 ||
                replica.SynchronizationState?.Length > 60 ||
                replica.SynchronizationHealth?.Length > 60 ||
                replica.SendQueueBytes is < 0 ||
                replica.RedoQueueBytes is < 0 ||
                replica.LagSeconds is < 0))
        {
            throw new InvalidDataException("Invalid HA evidence row.");
        }

        return new HaHealthSnapshot(
            row.IsHadrEnabled,
            row.TotalLocalDatabaseReplicas,
            replicas.Select(replica => new HaDatabaseReplicaSnapshot(
                replica.GroupKey,
                replica.DatabaseKey,
                replica.Role,
                replica.SynchronizationState,
                replica.SynchronizationHealth,
                replica.Connected,
                replica.AutomaticFailover,
                replica.IsSuspended,
                replica.SendQueueBytes,
                replica.RedoQueueBytes,
                replica.LagSeconds)).ToArray(),
            row.QuorumState,
            row.HealthyVotes,
            row.TotalVotes,
            row.TotalLocalDatabaseReplicas > HaSnapshotQuery.MaxReplicas);
    }
}

internal sealed class HaSnapshotQuery(PerformanceScaleOptions? performance = null)
{
    internal const int MaxReplicas = 50;
    internal const string CommandText = """
        SELECT
            CONVERT(bit, COALESCE(SERVERPROPERTY(N'IsHadrEnabled'), 0)) AS IsHadrEnabled,
            (SELECT COUNT(*) FROM sys.dm_hadr_database_replica_states WHERE is_local = 1) AS TotalLocalDatabaseReplicas,
            (SELECT TOP (50)
                  CONVERT(nvarchar(128), ag.name) AS GroupKey,
                  CONVERT(nvarchar(128), d.name) AS DatabaseKey,
                  CONVERT(nvarchar(60), ars.role_desc) AS Role,
                  CONVERT(nvarchar(60), drs.synchronization_state_desc) AS SynchronizationState,
                  CONVERT(nvarchar(60), drs.synchronization_health_desc) AS SynchronizationHealth,
                  CASE WHEN ars.connected_state_desc IS NULL THEN NULL
                       WHEN ars.connected_state_desc = N'CONNECTED' THEN CONVERT(bit, 1)
                       ELSE CONVERT(bit, 0) END AS Connected,
                  CONVERT(bit, CASE WHEN ar.failover_mode_desc = N'AUTOMATIC' THEN 1 ELSE 0 END) AS AutomaticFailover,
                  CONVERT(bit, drs.is_suspended) AS IsSuspended,
                  CASE WHEN drs.log_send_queue_size IS NULL THEN NULL ELSE CONVERT(bigint, drs.log_send_queue_size) * 1024 END AS SendQueueBytes,
                  CASE WHEN drs.redo_queue_size IS NULL THEN NULL ELSE CONVERT(bigint, drs.redo_queue_size) * 1024 END AS RedoQueueBytes,
                  CASE WHEN drs.secondary_lag_seconds IS NULL THEN NULL ELSE CONVERT(bigint, drs.secondary_lag_seconds) END AS LagSeconds
              FROM sys.dm_hadr_database_replica_states AS drs
              INNER JOIN sys.availability_replicas AS ar ON ar.replica_id = drs.replica_id
              INNER JOIN sys.availability_groups AS ag ON ag.group_id = ar.group_id
              INNER JOIN sys.databases AS d ON d.database_id = drs.database_id
              LEFT JOIN sys.dm_hadr_availability_replica_states AS ars
                ON ars.replica_id = drs.replica_id AND ars.is_local = 1
              WHERE drs.is_local = 1
              ORDER BY
                  CASE WHEN drs.synchronization_state_desc = N'SYNCHRONIZED' THEN 1 ELSE 0 END ASC,
                  CASE WHEN drs.synchronization_health_desc = N'HEALTHY' THEN 1 ELSE 0 END ASC,
                  COALESCE(drs.log_send_queue_size, 0) + COALESCE(drs.redo_queue_size, 0) DESC,
                  ag.name ASC,
                  d.name ASC
              FOR JSON PATH) AS ReplicasJson,
            (SELECT TOP (1) CONVERT(nvarchar(60), quorum_state_desc) FROM sys.dm_hadr_cluster) AS QuorumState,
            (SELECT CONVERT(int, SUM(CASE WHEN member_state_desc = N'UP' THEN number_of_quorum_votes ELSE 0 END)) FROM sys.dm_hadr_cluster_members) AS HealthyVotes,
            (SELECT CONVERT(int, SUM(number_of_quorum_votes)) FROM sys.dm_hadr_cluster_members) AS TotalVotes;
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
                ReadReplicas(reader, 2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5));
        }
        catch (SqlException exception)
        {
            throw new SqlProbeException(SqlErrorClassifier.Classify(exception.Number));
        }
    }

    private static IReadOnlyList<SqlHaReplicaRow> ReadReplicas(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return [];
        var json = reader.GetString(ordinal);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<SqlHaReplicaRow[]>(json) ?? [];
    }
}
