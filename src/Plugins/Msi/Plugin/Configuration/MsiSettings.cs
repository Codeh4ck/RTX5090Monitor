using System.Text.Json;
using RTXLens.Contracts.Configuration;
using RTXLens.Plugins.Msi.Contracts.Configuration;
using RTXLens.Plugins.Msi.Serialization;

namespace RTXLens.Plugins.Msi.Configuration;

public static class MsiSettings
{
    public static MsiOptions GetOptions(PluginSettings settings)
    {
        var options = settings.Options is { } json
            ? json.Deserialize(MsiJsonContext.Default.MsiOptions) ?? throw new ArgumentException("MSI options cannot be null.")
            : new MsiOptions();

        options.Validate();

        if (options.FanControl == MsiFanControl.Curve && settings.PollIntervalMs > 5000)
        {
            throw new ArgumentException("MSI fan curve control requires a polling interval of 1000-5000 ms.");
        }

        return settings.PollIntervalMs < 1000 ? throw new ArgumentException("MSI polling must be at least 1000 ms.") : options;
    }

    public static PluginSettings WithOptions(PluginSettings settings, MsiOptions options) =>
        settings with { Options = JsonSerializer.SerializeToElement(options, MsiJsonContext.Default.MsiOptions) };
}