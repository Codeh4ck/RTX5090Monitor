using System.Text.Json;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Serialization;

namespace Rtx5090Monitoring.Plugins.Msi.Configuration;

public static class MsiSettings
{
    public static MsiOptions GetOptions(PluginSettings settings)
    {
        var options = settings.Options is { } json
            ? json.Deserialize(MsiJsonContext.Default.MsiOptions) ?? throw new ArgumentException("MSI options cannot be null.")
            : new MsiOptions();

        options.Validate();

        return settings.PollIntervalMs < 1000 ? throw new ArgumentException("MSI polling must be at least 1000 ms.") : options;
    }

    public static PluginSettings WithOptions(PluginSettings settings, MsiOptions options) =>
        settings with { Options = JsonSerializer.SerializeToElement(options, MsiJsonContext.Default.MsiOptions) };
}