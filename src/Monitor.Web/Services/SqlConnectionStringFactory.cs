using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal static class SqlConnectionStringFactory
{
    public static string Create(
        ServerRegistration registration,
        SqlLoginSecret? secret,
        string applicationName)
    {
        var endpoint = registration.Endpoint;
        var dataSource = endpoint.Port.HasValue
            ? $"{endpoint.Host},{endpoint.Port.Value}"
            : endpoint.InstanceName is not null
                ? $"{endpoint.Host}\\{endpoint.InstanceName}"
                : endpoint.Host;

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = dataSource,
            InitialCatalog = "master",
            Encrypt = endpoint.Encrypt,
            TrustServerCertificate = endpoint.TrustServerCertificate,
            IntegratedSecurity = registration.AuthenticationMode == SqlAuthenticationMode.IntegratedSecurity,
            ConnectTimeout = 5,
            ApplicationName = applicationName,
            Pooling = false
        };

        if (secret is not null)
        {
            builder.UserID = secret.Username;
            builder.Password = secret.Password;
        }

        return builder.ConnectionString;
    }
}
