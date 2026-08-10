using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Monitor.Web.Services;

public sealed class AdminCredentialOptions
{
    public const string SectionName = "DevelopmentAdmin";

    public string Username { get; set; } = string.Empty;
    public int Iterations { get; set; } = 120_000;
    public string SaltBase64 { get; set; } = string.Empty;
    public string HashBase64 { get; set; } = string.Empty;
}

public interface IAdminCredentialVerifier
{
    bool Verify(string username, string password);
}

public sealed class AdminCredentialVerifier : IAdminCredentialVerifier
{
    private readonly AdminCredentialOptions _options;

    public AdminCredentialVerifier(IOptions<AdminCredentialOptions> options)
    {
        _options = options.Value;
    }

    public bool Verify(string username, string password)
    {
        if (!string.Equals(username, _options.Username, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(_options.SaltBase64);
            var expectedHash = Convert.FromBase64String(_options.HashBase64);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                _options.Iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
