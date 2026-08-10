using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal static class ConfiguredServerRegistrationLoader
{
    private const string SectionName = "Monitor:PrimaryServer";

    public static ServerRegistration? Load(IConfiguration configuration, TimeProvider timeProvider)
    {
        var section = configuration.GetSection(SectionName);
        if (!section.Exists())
        {
            return null;
        }

        var id = Guid.Parse(section["Id"] ?? throw new InvalidOperationException($"{SectionName}:Id is required."));
        var displayName = section["DisplayName"] ?? throw new InvalidOperationException($"{SectionName}:DisplayName is required.");
        var host = section["Host"] ?? throw new InvalidOperationException($"{SectionName}:Host is required.");
        var authenticationMode = Enum.Parse<SqlAuthenticationMode>(
            section["AuthenticationMode"] ?? nameof(SqlAuthenticationMode.IntegratedSecurity),
            ignoreCase: true);
        var secretValue = section["SecretReference"];
        var secretReference = string.IsNullOrWhiteSpace(secretValue)
            ? (ConnectionSecretReference?)null
            : new ConnectionSecretReference(secretValue);

        return new ServerRegistration(
            id,
            displayName,
            new SqlServerEndpoint(
                host,
                section.GetValue<int?>("Port"),
                section["InstanceName"],
                section.GetValue("Encrypt", true),
                section.GetValue("TrustServerCertificate", false)),
            authenticationMode,
            secretReference,
            section.GetValue("Enabled", true),
            timeProvider.GetUtcNow());
    }
}
