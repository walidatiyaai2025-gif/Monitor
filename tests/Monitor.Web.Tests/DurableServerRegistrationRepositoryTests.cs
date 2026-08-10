using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DurableServerRegistrationRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"monitor-registration-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Upsert_ReloadsRegistrationAfterRepositoryRestart()
    {
        var path = StorePath();
        var registration = Integrated("Beta SQL", "sql-beta.internal");
        var first = new FileServerRegistrationRepository(path);

        first.Upsert(registration);
        var restarted = new FileServerRegistrationRepository(path);

        var loaded = Assert.Single(restarted.GetAll());
        Assert.Equal(registration.Id, loaded.Id);
        Assert.Equal("Beta SQL", loaded.DisplayName);
        Assert.Equal("sql-beta.internal", loaded.Endpoint.Host);
        Assert.Equal(SqlAuthenticationMode.IntegratedSecurity, loaded.AuthenticationMode);
        Assert.Null(loaded.SecretReference);
        Assert.Equal(registration.CreatedAtUtc, loaded.CreatedAtUtc);
    }

    [Fact]
    public void SqlLogin_PersistsOnlyOpaqueReferenceAndNeverCredentialValues()
    {
        var path = StorePath();
        const string opaqueReference = "external-prod-sql-01";
        const string canaryUsername = "CANARY-USERNAME-MUST-NOT-APPEAR";
        const string canaryPassword = "CANARY-PASSWORD-MUST-NOT-APPEAR";
        var registration = SqlLogin("Production SQL", "sql-prod.internal", opaqueReference);
        var repository = new FileServerRegistrationRepository(path);

        repository.Upsert(registration);
        var persistedText = File.ReadAllText(path);
        var restarted = new FileServerRegistrationRepository(path);
        var loaded = Assert.Single(restarted.GetAll());

        Assert.Equal(opaqueReference, loaded.SecretReference?.Value);
        Assert.Contains(opaqueReference, persistedText, StringComparison.Ordinal);
        Assert.DoesNotContain(canaryUsername, persistedText, StringComparison.Ordinal);
        Assert.DoesNotContain(canaryPassword, persistedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", persistedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Username", persistedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Remove_IsPersistedAcrossRepositoryRestart()
    {
        var path = StorePath();
        var first = Integrated("SQL One", "sql-one.internal");
        var second = Integrated("SQL Two", "sql-two.internal");
        var repository = new FileServerRegistrationRepository(path);
        repository.Upsert(first);
        repository.Upsert(second);

        Assert.True(repository.Remove(first.Id));
        var restarted = new FileServerRegistrationRepository(path);

        var loaded = Assert.Single(restarted.GetAll());
        Assert.Equal(second.Id, loaded.Id);
        Assert.Null(restarted.GetById(first.Id));
    }

    [Fact]
    public void GetAll_RemainsDeterministicallyOrderedAfterRestart()
    {
        var path = StorePath();
        var repository = new FileServerRegistrationRepository(path);
        repository.Upsert(Integrated("Zulu", "zulu.internal"));
        repository.Upsert(Integrated("alpha", "alpha.internal"));
        repository.Upsert(Integrated("Beta", "beta.internal"));

        var restarted = new FileServerRegistrationRepository(path);

        Assert.Equal(["alpha", "Beta", "Zulu"], restarted.GetAll().Select(item => item.DisplayName).ToArray());
    }

    [Fact]
    public void CorruptStore_FailsClosedInsteadOfStartingEmpty()
    {
        var path = StorePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this-is-not-valid-json");

        var exception = Assert.Throws<InvalidDataException>(() => new FileServerRegistrationRepository(path));

        Assert.Contains("corrupt", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidPersistedDomainData_FailsClosed()
    {
        var path = StorePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
        {
          "version": 1,
          "registrations": [
            {
              "id": "{{Guid.NewGuid()}}",
              "displayName": "Broken SQL",
              "host": "sql.internal",
              "port": 1433,
              "instanceName": "MSSQLSERVER",
              "encrypt": true,
              "trustServerCertificate": false,
              "authenticationMode": 0,
              "secretReference": null,
              "isEnabled": true,
              "createdAtUtc": "2026-08-10T10:00:00+00:00"
            }
          ]
        }
        """);

        Assert.Throws<InvalidDataException>(() => new FileServerRegistrationRepository(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string StorePath() => Path.Combine(_directory, "registrations.json");

    private static ServerRegistration Integrated(string displayName, string host) =>
        new(
            Guid.NewGuid(),
            displayName,
            new SqlServerEndpoint(host),
            SqlAuthenticationMode.IntegratedSecurity,
            secretReference: null,
            isEnabled: true,
            createdAtUtc: new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero));

    private static ServerRegistration SqlLogin(string displayName, string host, string secretReference) =>
        new(
            Guid.NewGuid(),
            displayName,
            new SqlServerEndpoint(host),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference(secretReference),
            isEnabled: true,
            createdAtUtc: new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero));
}
