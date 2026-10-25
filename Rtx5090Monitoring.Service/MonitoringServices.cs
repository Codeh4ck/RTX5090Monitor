using Rtx5090Monitoring.Core.Configuration;
using Rtx5090Monitoring.Core;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Core.Streaming;
using Rtx5090Monitoring.Service.Configuration;
using Rtx5090Monitoring.Service.Services;
using Rtx5090Monitoring.Service.Utilities;

namespace Rtx5090Monitoring.Service;

public static class MonitoringServices
{
    public static IServiceCollection AddMonitoringCore(this IServiceCollection services, IConfiguration configuration)
    {
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Rtx5090Monitoring", "appsettings.json");
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
        services.AddHostedService<SnapshotBroadcastService>();
        services.AddSingleton<IServiceRestart, ServiceRestart>();

        return services;
    }
}
