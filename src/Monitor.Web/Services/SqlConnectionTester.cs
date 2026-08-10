using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface ISqlConnectionTester
{
    Task<ConnectionTestResult> TestAsync(Guid registrationId, CancellationToken cancellationToken = default);
}

internal sealed record ConnectionProfileBuildResult(
    string? ConnectionString,
    ConnectionTestStatus? FailureStatus)
{
    public bool Success => ConnectionString is not null && FailureStatus is null;
}

internal interface IConnectionProfileFactory
{
    ValueTask<ConnectionProfileBuildResult> BuildAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default);
}

internal sealed class SqlConnectionProfileFactory(IConnectionSecretStore secretStore) : IConnectionProfileFactory
{
    public async ValueTask<ConnectionProfileBuildResult> BuildAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var endpoint = registration.Endpoint;
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = BuildDataSource(endpoint),
            InitialCatalog = "master",
            ApplicationName = "Monitor",
            IntegratedSecurity = registration.AuthenticationMode == SqlAuthenticationMode.IntegratedSecurity,
            Encrypt = endpoint.Encrypt,
            TrustServerCertificate = endpoint.TrustServerCertificate,
            ConnectTimeout = 5,
            ConnectRetryCount = 0,
            Pooling = false,
            PersistSecurityInfo = false
        };

        if (registration.AuthenticationMode == SqlAuthenticationMode.SqlLogin)
        {
            if (registration.SecretReference is null)
            {
                return new ConnectionProfileBuildResult(null, ConnectionTestStatus.SecretUnavailable);
            }

            var secret = await secretStore.ResolveAsync(registration.SecretReference.Value, cancellationToken);
            if (secret is null)
            {
                return new ConnectionProfileBuildResult(null, ConnectionTestStatus.SecretUnavailable);
            }

            builder.UserID = secret.Username;
            builder.Password = secret.Password;
        }

        return new ConnectionProfileBuildResult(builder.ConnectionString, null);
    }

    private static string BuildDataSource(SqlServerEndpoint endpoint)
    {
        if (endpoint.Port.HasValue)
        {
            return $"{endpoint.Host},{endpoint.Port.Value}";
        }

        return endpoint.InstanceName is null
            ? endpoint.Host
            : $"{endpoint.Host}\\{endpoint.InstanceName}";
    }
}

internal enum SqlProbeFailureKind
{
    Authentication,
    Timeout,
    Network,
    Certificate,
    InvalidConfiguration,
    Unexpected
}

internal sealed record SqlProbeOutcome(
    bool Success,
    string? DataSource = null,
    string? ServerVersion = null,
    SqlProbeFailureKind? FailureKind = null);

internal interface ISqlConnectionProbe
{
    Task<SqlProbeOutcome> OpenAsync(string connectionString, CancellationToken cancellationToken);
}

internal sealed class SqlClientConnectionProbe : ISqlConnectionProbe
{
    public async Task<SqlProbeOutcome> OpenAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return new SqlProbeOutcome(true, connection.DataSource, connection.ServerVersion);
        }
        catch (SqlException exception) when (exception.Number == 18456)
        {
            return new SqlProbeOutcome(false, FailureKind: SqlProbeFailureKind.Authentication);
        }
        catch (SqlException exception) when (exception.Number == -2)
        {
            return new SqlProbeOutcome(false, FailureKind: SqlProbeFailureKind.Timeout);
        }
        catch (SqlException exception) when (LooksLikeCertificateFailure(exception.Message))
        {
            return new SqlProbeOutcome(false, FailureKind: SqlProbeFailureKind.Certificate);
        }
        catch (SqlException)
        {
            return new SqlProbeOutcome(false, FailureKind: SqlProbeFailureKind.Network);
        }
        catch (OperationCanceledException)
        {
            return new SqlProbeOutcome(false, FailureKind: SqlProbeFailureKind.Timeout);
        }
        catch (ArgumentException)
        {
            return new SqlProbeOutcome(false, FailureKind: SqlProbeFailureKind.InvalidConfiguration);
        }
        catch (InvalidOperationException)
        {
            return new SqlProbeOutcome(false, FailureKind: SqlProbeFailureKind.InvalidConfiguration);
        }
        catch
        {
            return new SqlProbeOutcome(false, FailureKind: SqlProbeFailureKind.Unexpected);
        }
    }

    private static bool LooksLikeCertificateFailure(string message) =>
        message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("TLS", StringComparison.OrdinalIgnoreCase);
}

public sealed class SqlConnectionTester(
    IServerRegistrationRepository registrations,
    IConnectionProfileFactory profileFactory,
    ISqlConnectionProbe probe) : ISqlConnectionTester
{
    public async Task<ConnectionTestResult> TestAsync(
        Guid registrationId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var registration = registrations.GetById(registrationId);
        if (registration is null)
        {
            return Result(ConnectionTestStatus.RegistrationNotFound, "The registered SQL target no longer exists.", stopwatch);
        }

        if (!registration.IsEnabled)
        {
            return Result(ConnectionTestStatus.RegistrationDisabled, "This SQL target is disabled and was not contacted.", stopwatch);
        }

        ConnectionProfileBuildResult profile;
        try
        {
            profile = await profileFactory.BuildAsync(registration, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result(ConnectionTestStatus.Timeout, "The connection test was cancelled or exceeded its allowed time.", stopwatch);
        }
        catch
        {
            return Result(ConnectionTestStatus.InvalidConfiguration, "The connection profile could not be prepared safely.", stopwatch);
        }

        if (!profile.Success)
        {
            return Result(
                profile.FailureStatus ?? ConnectionTestStatus.InvalidConfiguration,
                "The required SQL login secret is unavailable. Check the external secret configuration.",
                stopwatch);
        }

        var outcome = await probe.OpenAsync(profile.ConnectionString!, cancellationToken);
        if (outcome.Success)
        {
            return new ConnectionTestResult(
                ConnectionTestStatus.Success,
                "Connection established successfully.",
                stopwatch.ElapsedMilliseconds,
                outcome.DataSource,
                outcome.ServerVersion);
        }

        var (status, message) = outcome.FailureKind switch
        {
            SqlProbeFailureKind.Authentication => (ConnectionTestStatus.AuthenticationFailed, "SQL Server rejected the supplied identity."),
            SqlProbeFailureKind.Timeout => (ConnectionTestStatus.Timeout, "The SQL Server did not complete the connection within the allowed time."),
            SqlProbeFailureKind.Certificate => (ConnectionTestStatus.CertificateFailure, "TLS certificate validation prevented the connection."),
            SqlProbeFailureKind.InvalidConfiguration => (ConnectionTestStatus.InvalidConfiguration, "The SQL connection profile is invalid."),
            SqlProbeFailureKind.Network => (ConnectionTestStatus.NetworkFailure, "The SQL Server could not be reached or opened."),
            _ => (ConnectionTestStatus.UnexpectedFailure, "The connection test failed without exposing internal connection details.")
        };

        return Result(status, message, stopwatch);
    }

    private static ConnectionTestResult Result(
        ConnectionTestStatus status,
        string message,
        Stopwatch stopwatch) => new(status, message, stopwatch.ElapsedMilliseconds);
}
