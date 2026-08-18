using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class BackupAssemblyBudgetTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CollectHistory_StopsReadingFurtherRegistrationsOnceBundleCannotFit()
    {
        var registrations = Enumerable.Range(0, 100)
            .Select(index => Registration(GuidFrom(index)))
            .ToArray();
        var budget = new BackupAssemblyBudget(maxBundleBytes: 1_500);
        var readCalls = 0;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            budget.CollectHistory(registrations, registrationId =>
            {
                readCalls++;
                return Enumerable.Range(0, 50)
                    .Select(index => Point(registrationId, index))
                    .ToArray();
            }));

        Assert.Contains("bundle size limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(readCalls, 1, registrations.Length - 1);
    }

    [Fact]
    public void CollectHistory_WithinBudgetPreservesDeterministicRegistrationAndTimeOrdering()
    {
        var firstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var registrations = new[] { Registration(secondId), Registration(firstId) };
        var budget = new BackupAssemblyBudget(maxBundleBytes: 64 * 1024);

        var history = budget.CollectHistory(registrations, registrationId =>
            [Point(registrationId, 2), Point(registrationId, 0), Point(registrationId, 1)]);

        Assert.Equal(6, history.Length);
        Assert.Equal(
            new[]
            {
                (firstId, Epoch),
                (firstId, Epoch.AddMinutes(1)),
                (firstId, Epoch.AddMinutes(2)),
                (secondId, Epoch),
                (secondId, Epoch.AddMinutes(1)),
                (secondId, Epoch.AddMinutes(2))
            },
            history.Select(item => (item.RegistrationId, item.CollectedAtUtc)).ToArray());
    }

    [Fact]
    public void Admit_RejectsOnlyAfterCompactSerializedItemsAloneExceedBudget()
    {
        var budget = new BackupAssemblyBudget(maxBundleBytes: 128);

        budget.Admit("small");
        var exception = Assert.Throws<InvalidOperationException>(() => budget.Admit(new string('x', 256)));

        Assert.Contains("bundle size limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static BackupRegistration Registration(Guid id) => new(
        id,
        "SQL",
        "sql.internal",
        1433,
        null,
        true,
        false,
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        Epoch.AddHours(-1));

    private static SnapshotHistoryPoint Point(Guid registrationId, int minute) => new(
        registrationId,
        Epoch.AddMinutes(minute),
        5,
        5,
        42,
        0,
        1,
        SnapshotFreshness.Fresh);

    private static Guid GuidFrom(int index)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(index + 1).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}
