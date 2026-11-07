using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Service;

internal static class TestHostConfigurationTests
{
    public static async Task RunAsync(TestRunner runner) => await runner.RunAsync(IgnoresInheritedPluginConfigurationAsync);

    private static async Task IgnoresInheritedPluginConfigurationAsync()
    {
        const string variable = "Plugins__unexpected-test-plugin__Enabled";
        var previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "true");

        try
        {
            await using var host = await MonitoringTestHost.StartWithoutPluginsAsync();
            var configuration = await host.GetConfigurationAsync();
            var health = await host.GetHealthAsync();

            TestAssert.Equal(0, configuration.Configuration.Plugins.Count);
            TestAssert.Equal(100, configuration.Configuration.Monitoring.StreamIntervalMs);
            TestAssert.Equal("running", health.GetProperty("status").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }
}
