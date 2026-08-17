using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class BackupPolicyOptionsTests
{
    [Fact]
    public void DefaultPolicy_IsDisabledAndHasNoInventedRpo()
    {
        var options = new BackupPolicyOptions();

        options.Validate();

        Assert.False(options.Enabled);
        Assert.False(options.IsConfigured);
        Assert.Null(options.FullRpoMinutes);
        Assert.Null(options.LogRpoMinutes);
        Assert.Null(options.FullRpo);
        Assert.Null(options.LogRpo);
    }

    [Theory]
    [InlineData(1440, null)]
    [InlineData(null, 15)]
    [InlineData(null, null)]
    public void EnabledPolicy_RequiresBothExplicitRpoValues(int? fullMinutes, int? logMinutes)
    {
        var options = new BackupPolicyOptions
        {
            Enabled = true,
            FullRpoMinutes = fullMinutes,
            LogRpoMinutes = logMinutes
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("does not invent backup RPO values", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(-1, 15)]
    [InlineData(1440, 0)]
    [InlineData(1440, -5)]
    public void ConfiguredRpoValues_MustBePositive(int fullMinutes, int logMinutes)
    {
        var options = new BackupPolicyOptions
        {
            Enabled = false,
            FullRpoMinutes = fullMinutes,
            LogRpoMinutes = logMinutes
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void EnabledPolicy_ProjectsExplicitMinutesWithoutChangingUnits()
    {
        var options = new BackupPolicyOptions
        {
            Enabled = true,
            FullRpoMinutes = 1440,
            LogRpoMinutes = 15
        };

        options.Validate();

        Assert.True(options.IsConfigured);
        Assert.Equal(TimeSpan.FromDays(1), options.FullRpo);
        Assert.Equal(TimeSpan.FromMinutes(15), options.LogRpo);
    }
}
