using System.Data;
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
                    reader.GetString(13)),
                new SqlHealthModulesRow(
                    reader.GetInt64(14), reader.GetInt64(15), reader.GetInt64(16), reader.GetInt64(17), reader.GetInt64(18), reader.GetInt64(19),
                    reader.GetInt64(20), reader.GetInt64(21), reader.IsDBNull(22) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(22), DateTimeKind.Utc)),
                    reader.GetInt64(23), reader.GetInt64(24), reader.GetInt64(25),
                    reader.GetInt64(26), reader.GetInt64(27), reader.GetInt64(28),
                    reader.GetInt64(29), reader.GetInt64(30)),
                new SqlPerformanceRow(reader.GetInt64(31), reader.GetInt64(32), reader.GetInt64(33)));
        }
        catch (SqlException exception)
        {
            throw new SqlProbeException(SqlErrorClassifier.Classify(exception.Number));
        }
    }
}
