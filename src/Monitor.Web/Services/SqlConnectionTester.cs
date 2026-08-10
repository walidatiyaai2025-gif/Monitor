using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal enum SqlProbeFailureKind
{
    Authentication,
    Timeout,
    Network,
    Certificate,
    Other
}

internal sealed class SqlProbeException(SqlProbeFailureKind kind) : Exception
{
    public SqlProbeFailureKind Kind { get; } = kind;
}

internal static class SqlErrorClassifier
{
    public static SqlProbeFailureKind Classify(int number) => number switch
    {
        18456 => SqlProbeFailureKind.Authentication,
        -2146893019 or -2146893022 => SqlProbeFailureKind.Certificate,
        -2 => SqlProbeFailureKind.Timeout,
        -1 or 2 or 53 or 11001 => SqlProbeFailureKind.Network,
        _ => SqlProbeFailureKind.Other
    };
}

internal sealed record SqlProbeResult(string? ServerVersion);

internal interface ISqlConnectionProbe
{
    Task<SqlProbeResult> ProbeAsync(
        ServerRegistration registration,
        SqlLoginSecret? secret,
        CancellationToken cancellationToken);
}

public interface IServerConnectionTester
{
    Task<ConnectionTestResult> TestAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default);
}

internal sealed class ServerConnectionTester(
    IConnectionSecretStore secretStore,
    ISqlConnectionProbe probe) : IServerConnectionTester
{
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(7);

    public async Task<ConnectionTestResult> TestAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var stopwatch = Stopwatch.StartNew();

        if (!registration.IsEnabled)
        {
            return Result(ConnectionTestStatus.Disabled, "This server registration is disabled.", stopwatch);
        }

        SqlLoginSecret? secret = null;
        if (registration.AuthenticationMode == SqlAuthenticationMode.SqlLogin)
        {
            secret = await secretStore.ResolveAsync(registration.SecretReference!.Value, cancellationToken);
            if (secret is null)
            {
                return Result(ConnectionTestStatus.SecretUnavailable, "Connection credentials are unavailable.", stopwatch);
            }
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(OverallTimeout);

        try
        {
            var probeResult = await probe.ProbeAsync(registration, secret, timeoutSource.Token);
            return Result(
                ConnectionTestStatus.Succeeded,
                "Connection succeeded.",
                stopwatch,
                probeResult.ServerVersion);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result(ConnectionTestStatus.TimedOut, "Connection timed out.", stopwatch);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlProbeException exception)
        {
            return exception.Kind switch
            {
                SqlProbeFailureKind.Authentication => Result(ConnectionTestStatus.AuthenticationFailed, "Authentication failed.", stopwatch),
                SqlProbeFailureKind.Timeout => Result(ConnectionTestStatus.TimedOut, "Connection timed out.", stopwatch),
                SqlProbeFailureKind.Network => Result(ConnectionTestStatus.NetworkUnavailable, "The SQL Server could not be reached.", stopwatch),
                SqlProbeFailureKind.Certificate => Result(ConnectionTestStatus.CertificateRejected, "SQL Server certificate validation failed.", stopwatch),
                _ => Result(ConnectionTestStatus.Failed, "Connection failed.", stopwatch)
            };
        }
        catch (Exception)
        {
            return Result(ConnectionTestStatus.Failed, "Connection failed.", stopwatch);
        }
    }

    private static ConnectionTestResult Result(
        ConnectionTestStatus status,
        string message,
        Stopwatch stopwatch,
        string? version = null) =>
        new(status, message, stopwatch.ElapsedMilliseconds, version);
}

internal sealed class SqlConnectionProbe : ISqlConnectionProbe
{
    public async Task<SqlProbeResult> ProbeAsync(
        ServerRegistration registration,
        SqlLoginSecret? secret,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = SqlConnectionStringFactory.Create(
                registration,
                secret,
                "Monitor/TestConnection");
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))";
            command.CommandTimeout = 2;
            var version = await command.ExecuteScalarAsync(cancellationToken) as string;
            return new SqlProbeResult(version);
        }
        catch (SqlException exception)
        {
            throw new SqlProbeException(SqlErrorClassifier.Classify(exception.Number));
        }
    }
}
