using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class PersistenceModeValidationTests
{
    [Fact]
    public void RegistrationStore_ValidModesRemainSupported()
    {
        new RegistrationStoreOptions
        {
            Mode = RegistrationStoreMode.File,
            Path = "App_Data/registrations.json"
        }.Validate();

        new RegistrationStoreOptions
        {
            Mode = RegistrationStoreMode.InMemory,
            Path = string.Empty
        }.Validate();
    }

    [Fact]
    public void RegistrationStore_UndefinedModeFailsClosed()
    {
        var options = new RegistrationStoreOptions
        {
            Mode = (RegistrationStoreMode)999,
            Path = "App_Data/registrations.json"
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("Mode", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OperationalStore_ValidModesRemainSupported()
    {
        new OperationalStoreOptions
        {
            Mode = OperationalStoreMode.File,
            RootPath = "App_Data/operational"
        }.Validate();

        new OperationalStoreOptions
        {
            Mode = OperationalStoreMode.InMemory,
            RootPath = string.Empty
        }.Validate();
    }

    [Fact]
    public void OperationalStore_UndefinedModeFailsClosedBeforePersistenceSelection()
    {
        var options = new OperationalStoreOptions
        {
            Mode = (OperationalStoreMode)999,
            RootPath = "App_Data/operational"
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("Mode", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
