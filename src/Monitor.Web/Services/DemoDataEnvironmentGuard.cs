using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Monitor.Web.Services;

public static class DemoDataEnvironmentGuard
{
    public static void Validate(IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        if (environment.IsDevelopment()) return;
        if (!configuration.GetValue("DemoData:Enabled", false)) return;

        throw new InvalidOperationException(
            "DemoData:Enabled must remain false outside the Development environment.");
    }
}
