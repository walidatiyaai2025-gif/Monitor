using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800NoFakeDataAcceptanceTests
{
    private static readonly string Root = FindRoot();

    [Theory]
    [InlineData(Environments.Production)]
    [InlineData(Environments.Staging)]
    [InlineData("Acceptance")]
    public void DemoDataGuard_RejectsEnabledDemoOutsideDevelopment(string environmentName)
    {
        var configuration = Configuration(enabled: true);
        var environment = new TestHostEnvironment(environmentName);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DemoDataEnvironmentGuard.Validate(environment, configuration));

        Assert.Contains("must remain false outside the Development environment", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DemoDataGuard_AllowsExplicitDevelopmentDemo()
    {
        DemoDataEnvironmentGuard.Validate(
            new TestHostEnvironment(Environments.Development),
            Configuration(enabled: true));
    }

    [Theory]
    [InlineData(Environments.Production)]
    [InlineData(Environments.Staging)]
    [InlineData(Environments.Development)]
    public void DemoDataGuard_AllowsDisabledDemoInEveryEnvironment(string environmentName)
    {
        DemoDataEnvironmentGuard.Validate(
            new TestHostEnvironment(environmentName),
            Configuration(enabled: false));
    }

    [Fact]
    public void DemoService_DisabledConfigurationNeverReturnsSyntheticEstate()
    {
        var demo = new DemoMonitorService(Configuration(enabled: false));

        Assert.Empty(demo.GetServers());
        Assert.Empty(demo.GetIncidents());
        Assert.Null(demo.GetServer("da-sql01"));
        Assert.Empty(demo.GetDashboard().Servers);
        Assert.Empty(demo.GetDashboard().Incidents);
    }

    [Fact]
    public void Configuration_DefaultsOffAndDevelopmentOptsInExplicitly()
    {
        Assert.False(ReadDemoEnabled("src/Monitor.Web/appsettings.json"));
        Assert.True(ReadDemoEnabled("src/Monitor.Web/appsettings.Development.json"));
    }

    [Fact]
    public void Program_ValidatesDemoEnvironmentBeforeRegisteringDemoService()
    {
        var source = Read("src/Monitor.Web/Program.cs");
        const string guard = "DemoDataEnvironmentGuard.Validate(builder.Environment, builder.Configuration);";
        const string registration = "builder.Services.AddSingleton<IDemoMonitorService, DemoMonitorService>();";

        var guardIndex = source.IndexOf(guard, StringComparison.Ordinal);
        var registrationIndex = source.IndexOf(registration, StringComparison.Ordinal);

        Assert.True(guardIndex >= 0, "Program.cs must invoke the DemoData environment guard during startup.");
        Assert.True(registrationIndex >= 0, "Program.cs must keep the Development demo service registration explicit.");
        Assert.True(guardIndex < registrationIndex, "DemoData environment validation must run before the demo service can be resolved.");
    }

    [Fact]
    public void GuardSource_DoesNotDependOnSqlCollectionOrRuntimeMutation()
    {
        var source = Read("src/Monitor.Web/Services/DemoDataEnvironmentGuard.cs");

        foreach (var forbidden in new[]
        {
            "ISqlSnapshotQuery",
            "ISqlServerSnapshotCollector",
            "ISnapshotRefreshService",
            "SqlConnection",
            "IServerRegistrationRepository"
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    private static IConfiguration Configuration(bool enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoData:Enabled"] = enabled ? "true" : "false"
            })
            .Build();

    private static bool ReadDemoEnabled(string relative)
    {
        using var json = JsonDocument.Parse(Read(relative));
        return json.RootElement.GetProperty("DemoData").GetProperty("Enabled").GetBoolean();
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Monitor.Web.Tests";
        public string ContentRootPath { get; set; } = Root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
