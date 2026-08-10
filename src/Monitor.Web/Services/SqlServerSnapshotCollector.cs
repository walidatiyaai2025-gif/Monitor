using System.Data;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed record SqlSnapshotRow(
    string ServerName,
    string ProductVersion,
    string Edition,
    string? InstanceName,
    long UptimeSeconds,
    long DatabaseTotal,
    long DatabaseOnline);

internal interface ISqlSnapshotQuery
{
    Task<SqlSnapshotRow> ExecuteAsync(
        ServerRegistration registration,
        SqlLoginSecret? secret,
        CancellationToken cancellationToken);
}

public interface ISqlServerSnapshotCollector
{
    Task<SqlServerIdentitySnapshot> CollectAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default);
}

internal sealed class SqlServerSnapshotCollector(
    IConnectionSecretStore secretStore,
    ISqlSnapshotQuery query,
    TimeProvider timeProvider) : ISqlServerSnapshotCollector
{
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(7);

    public async Task<SqlServerIdentitySnapshot> CollectAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!registration.IsEnabled)
        {
            throw Failure(SnapshotCollectionFailure.Disabled, "Server registration is disabled.");
        }

        SqlLoginSecret? secret = null;
        if (registration.AuthenticationMode == SqlAuthenticationMode.SqlLogin)
        {
            secret = await secretStore.ResolveAsync(registration.SecretReference!.Value, cancellationToken);
            if (secret is null)
            {
                throw Failure(SnapshotCollectionFailure.SecretUnavailable, "Connection credentials are unavailable.");
            }
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(OverallTimeout);

        try
        {
            var row = await query.ExecuteAsync(registration, secret, timeoutSource.Token);
            var total = checked((int)row.DatabaseTotal);
            var online = checked((int)row.DatabaseOnline);
            if (row.UptimeSeconds < 0 || total < 0 || online < 0 || online > total)
            {
                throw new InvalidDataException("Invalid snapshot row.");
            }

            return new SqlServerIdentitySnapshot(
                registration.Id,
                row.ServerName,
                row.ProductVersion,
                row.Edition,
                row.InstanceName,
                row.UptimeSeconds,
                total,
                online,
                timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Failure(SnapshotCollectionFailure.TimedOut, "Snapshot collection timed out.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlProbeException exception)
        {
            throw exception.Kind switch
            {
                SqlProbeFailureKind.Authentication => Failure(SnapshotCollectionFailure.AuthenticationFailed, "Authentication failed."),
                SqlProbeFailureKind.Network => Failure(SnapshotCollectionFailure.NetworkUnavailable, "The SQL Server could not be reached."),
                SqlProbeFailureKind.Certificate => Failure(SnapshotCollectionFailure.CertificateRejected, "SQL Server certificate validation failed."),
                _ => Failure(SnapshotCollectionFailure.Failed, "Snapshot collection failed.")
            };
        }
        catch (SnapshotCollectionException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Failure(SnapshotCollectionFailure.Failed, "Snapshot collection failed.");
        }
    }

    private static SnapshotCollectionException Failure(
        SnapshotCollectionFailure failure,
        string message) => new(failure, message);
}

internal sealed class SqlSnapshotQuery : ISqlSnapshotQuery
{
    internal const string CommandText = """
        SELECT
            CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)) AS ServerName,
            CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
            CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition,
            CAST(SERVERPROPERTY('InstanceName') AS nvarchar(128)) AS InstanceName,
            DATEDIFF_BIG(SECOND, osi.sqlserver_start_time, SYSDATETIME()) AS UptimeSeconds,
            COUNT_BIG(*) AS DatabaseTotal,
            SUM(CASE WHEN d.state = 0 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DatabaseOnline
        FROM sys.databases AS d
        CROSS JOIN sys.dm_os_sys_info AS osi
        GROUP BY osi.sqlserver_start_time
        """;

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
                "Monitor/LightweightCollector");
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = CommandText;
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
                reader.GetInt64(6));
        }
        catch (SqlException exception)
        {
            throw new SqlProbeException(SqlErrorClassifier.Classify(exception.Number));
        }
    }
}
