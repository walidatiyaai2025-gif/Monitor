using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DurableServerRegistrationRepositoryTests : IDisposable
{
    private const int DefaultMaxStoreFileBytes = 16 * 1024 * 1024;
    private const int TestMaxStoreFileBytes = 2 * 1024;
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
    public void GetById_RefreshesStateWrittenByAnotherRepositoryInstance()
    {
        var path = StorePath();
        var staleReader = new FileServerRegistrationRepository(path);
        var writer = new FileServerRegistrationRepository(path);
        var registration = Integrated("Fresh SQL", "fresh.internal");

        writer.Upsert(registration);

        var loaded = staleReader.GetById(registration.Id);
        Assert.NotNull(loaded);
        Assert.Equal(registration.Id, loaded!.Id);
    }

    [Fact]
    public void Upsert_PreservesRegistrationWrittenByAnotherRepositoryInstance()
    {
        var path = StorePath();
        var firstWorker = new FileServerRegistrationRepository(path);
        var secondWorker = new FileServerRegistrationRepository(path);
        var first = Integrated("SQL One", "sql-one.internal");
        var second = Integrated("SQL Two", "sql-two.internal");

        firstWorker.Upsert(first);
        secondWorker.Upsert(second);

        var restarted = new FileServerRegistrationRepository(path);
        var loaded = restarted.GetAll();
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, item => item.Id == first.Id);
        Assert.Contains(loaded, item => item.Id == second.Id);
    }

    [Fact]
    public void TryReplaceSecretReference_RejectsStaleExpectedReferenceAcrossRepositoryInstances()
    {
        var path = StorePath();
        var oldReference = new ConnectionSecretReference("env:OLD");
        var winnerReference = new ConnectionSecretReference("env:WINNER");
        var staleReference = new ConnectionSecretReference("env:STALE");
        var registration = SqlLogin("Finance SQL", "finance.internal", oldReference.Value);
        var seed = new FileServerRegistrationRepository(path);
        seed.Upsert(registration);
        var winner = new FileServerRegistrationRepository(path);
        var staleWorker = new FileServerRegistrationRepository(path);

        Assert.True(winner.TryReplaceSecretReference(
            registration.Id,
            oldReference,
            winnerReference).Applied);

        var staleResult = staleWorker.TryReplaceSecretReference(
            registration.Id,
            oldReference,
            staleReference);

        Assert.Equal(ServerRegistrationFieldMutationStatus.Conflict, staleResult.Status);
        var restarted = new FileServerRegistrationRepository(path);
        Assert.Equal(winnerReference, restarted.GetById(registration.Id)!.SecretReference);
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
    public void OversizedRawStore_FailsClosedBeforeJsonParsing()
    {
        var path = StorePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(DefaultMaxStoreFileBytes + 1L);
        }

        var exception = Assert.Throws<InvalidDataException>(() => new FileServerRegistrationRepository(path));

        Assert.Contains("bounded file size", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OversizedPersistCandidate_RollsBackMemoryAndPreservesLastGoodFile()
    {
        var path = StorePath();
        var repository = new FileServerRegistrationRepository(path, TestMaxStoreFileBytes);
        var stable = Integrated("Stable SQL", "stable.internal");
        repository.Upsert(stable);
        var lastGoodBytes = File.ReadAllBytes(path);
        ServerRegistration? rejected = null;

        for (var index = 0; index < 12; index++)
        {
            var candidate = SqlLogin(
                new string((char)('A' + index), 120),
                new string((char)('a' + index), 255),
                $"external:{new string('s', 240)}:{index:D2}");
            try
            {
                repository.Upsert(candidate);
                lastGoodBytes = File.ReadAllBytes(path);
            }
            catch (InvalidOperationException exception)
            {
                Assert.Contains("bounded file size", exception.Message, StringComparison.OrdinalIgnoreCase);
                rejected = candidate;
                break;
            }
        }

        Assert.NotNull(rejected);
        Assert.Equal(lastGoodBytes, File.ReadAllBytes(path));
        Assert.Null(repository.GetById(rejected!.Id));
        Assert.NotNull(repository.GetById(stable.Id));

        var restarted = new FileServerRegistrationRepository(path, TestMaxStoreFileBytes);
        Assert.Null(restarted.GetById(rejected.Id));
        Assert.NotNull(restarted.GetById(stable.Id));
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
