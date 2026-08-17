using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed class GovernedSqlSnapshotQuery(PerformanceScaleOptions performance) : ISqlSnapshotQuery
{
    public async Task<SqlSnapshotRow> ExecuteAsync(
        ServerRegistration registration,
        SqlLoginSecret? secret,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = SqlConnectionStringFactory.Create(
                registration,
                secret,
                "Monitor/LightweightCollector",
                performance);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = SqlSnapshotQuery.CommandText;
            command.CommandTimeout = 2;
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidDataException("Snapshot query returned no row.");
            }

            return new SqlSnapshotRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetInt64(6),
                new SqlMemoryRow(
                    reader.GetInt64(7),
                    reader.GetInt64(8),
                    reader.GetInt64(9),
                    reader.GetInt32(10),
                    reader.GetBoolean(11),
                    reader.GetBoolean(12),
                    reader.GetString(13),
                    reader.IsDBNull(34) ? null : reader.GetInt64(34),
                    reader.IsDBNull(35) ? null : reader.GetInt64(35),
                    reader.IsDBNull(36) ? null : reader.GetInt64(36),
                    reader.IsDBNull(37) ? null : reader.GetInt64(37),
                    reader.IsDBNull(38) ? null : reader.GetInt64(38),
                    reader.IsDBNull(39) ? null : reader.GetString(39),
                    reader.IsDBNull(40) ? null : reader.GetInt64(40)),
                new SqlHealthModulesRow(
                    reader.GetInt64(14), reader.GetInt64(15), reader.GetInt64(16), reader.GetInt64(17), reader.GetInt64(18), reader.GetInt64(19),
                    reader.GetInt64(20), reader.GetInt64(21), reader.IsDBNull(22) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(22), DateTimeKind.Utc)),
                    reader.GetInt64(23), reader.GetInt64(24), reader.GetInt64(25),
                    reader.GetInt64(26), reader.GetInt64(27), reader.GetInt64(28),
                    reader.GetInt64(29), reader.GetInt64(30),
                    ReadIoFiles(reader, 42),
                    ReadAgentRuns(reader, 43),
                    ReadDatabaseStates(reader, 44),
                    ReadAgentSchedules(reader, 45)),
                new SqlPerformanceRow(
                    reader.GetInt64(31),
                    reader.GetInt64(32),
                    reader.GetInt64(33),
                    ReadWaitStats(reader, 41)));
        }
        catch (SqlException exception)
        {
            throw new SqlProbeException(SqlErrorClassifier.Classify(exception.Number));
        }
    }

    private static IReadOnlyList<SqlWaitStatRow> ReadWaitStats(SqlDataReader reader, int ordinal)
        => ReadJson<SqlWaitStatRow>(reader, ordinal);

    private static IReadOnlyList<SqlIoFileRow> ReadIoFiles(SqlDataReader reader, int ordinal)
        => ReadJson<SqlIoFileRow>(reader, ordinal);

    private static IReadOnlyList<SqlAgentRunRow> ReadAgentRuns(SqlDataReader reader, int ordinal)
        => ReadJson<SqlAgentRunRow>(reader, ordinal);

    private static IReadOnlyList<SqlDatabaseStateRow> ReadDatabaseStates(SqlDataReader reader, int ordinal)
        => ReadJson<SqlDatabaseStateRow>(reader, ordinal);

    private static IReadOnlyList<SqlAgentScheduleRow> ReadAgentSchedules(SqlDataReader reader, int ordinal)
        => ReadJson<SqlAgentScheduleRow>(reader, ordinal);

    private static IReadOnlyList<T> ReadJson<T>(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return [];
        var json = reader.GetString(ordinal);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<T[]>(json) ?? [];
    }
}
