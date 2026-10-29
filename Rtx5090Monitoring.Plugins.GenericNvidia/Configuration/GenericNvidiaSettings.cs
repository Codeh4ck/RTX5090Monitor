using System.Text.Json;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.GenericNvidia.Serialization;

namespace Rtx5090Monitoring.Plugins.GenericNvidia.Configuration;

public static class GenericNvidiaSettings
{
    public static GenericNvidiaOptions GetOptions(PluginSettings settings)
    {
        var options = settings.Options?.Deserialize(GenericNvidiaJsonContext.Default.GenericNvidiaOptions) ?? new GenericNvidiaOptions();
        options.Validate();
        return options;
    }

    public static PluginSettings WithOptions(PluginSettings settings, GenericNvidiaOptions options) => settings with
    {
        Options = JsonSerializer.SerializeToElement(options, GenericNvidiaJsonContext.Default.GenericNvidiaOptions)
    };
}
