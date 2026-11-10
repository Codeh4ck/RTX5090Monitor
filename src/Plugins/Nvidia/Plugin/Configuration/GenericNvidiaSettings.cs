using System.Text.Json;
using RTXLens.Contracts.Configuration;
using RTXLens.Plugins.GenericNvidia.Contracts.Configuration;
using RTXLens.Plugins.GenericNvidia.Serialization;

namespace RTXLens.Plugins.GenericNvidia.Configuration;

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
