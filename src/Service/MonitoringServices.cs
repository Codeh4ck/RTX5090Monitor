using RTXLens.Core.Configuration;
using RTXLens.Core;
using RTXLens.Core.Plugins;
using RTXLens.Core.Streaming;
using RTXLens.Service.Configuration;
using RTXLens.Service.Services;
using RTXLens.Service.Utilities;

namespace RTXLens.Service;

public static class MonitoringServices
{
    public static IServiceCollection AddMonitoringCore(this IServiceCollection services, IConfiguration configuration)
    {
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "RTXLens", "appsettings.json");
        var configurationPath = configuration["RuntimeConfiguration:FilePath"] ?? defaultPath;

        services.AddSingleton(new RuntimeConfigurationFile(Path.GetFullPath(configurationPath)));
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new MonitoringServiceLifetime(TimeProvider.System));
        services.AddSingleton(_ => new SensorStateStore([]));
        services.AddSingleton<SensorStreamHub>();
        services.AddSingleton<PluginRuntimeManager>();
        services.AddHostedService(provider => provider.GetRequiredService<PluginRuntimeManager>());
        services.AddSingleton<RuntimeConfigurationService>();
        services.AddSingleton<IRuntimeConfiguration>(provider => provider.GetRequiredService<RuntimeConfigurationService>());
        services.AddHostedService(provider => provider.GetRequiredService<RuntimeConfigurationService>());
        services.AddSingleton<ActiveSettingsProfileState>();
        services.AddSingleton<SettingsProfileStore>();
        services.AddSingleton<SettingsProfileResolver>();
        services.AddSingleton<DeviceWarningProfileStore>();
        services.AddSingleton<IWarningShutdown, WindowsWarningShutdown>();
        services.AddSingleton<WarningShutdownCoordinator>();
        services.AddSingleton<SafeguardJournalService>();
        services.AddHostedService(provider => provider.GetRequiredService<SafeguardJournalService>());
        services.AddHostedService<SnapshotBroadcastService>();
        services.AddHostedService<HardwareDiscoveryService>();
        services.AddSingleton<IServiceRestart, ServiceRestart>();

        return services;
    }
}
