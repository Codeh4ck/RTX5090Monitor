using Rtx5090Monitoring.Core.Plugins;

namespace Rtx5090Monitoring.Plugins.Asus.Services;

internal static class AsusAccess
{
    public static Task<T> ExecuteAsync<T>(PluginRuntimeManager runtime, Func<AsusPlugin, T> operation, CancellationToken token) =>
        runtime.ExecuteAsync(AsusPlugin.PluginId, plugin =>
        {
            var asus = (AsusPlugin)plugin;
            return asus.Latest is null ? throw new PluginUnavailableException("GPU sensor snapshot not available.") : operation(asus);
        }, token);
}
