using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rtx5090Monitoring.Hardware.Nvidia;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Asus.Factories;
using Rtx5090Monitoring.Plugins.Asus.Serialization;
using Rtx5090Monitoring.Plugins.Asus.Streaming;

namespace Rtx5090Monitoring.Plugins.Asus;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddAsusPlugin(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AsusJsonContext.Default));
        services.TryAddSingleton<INvidiaHardwareSource, LibreNvidiaHardwareSource>();
        services.TryAddSingleton<NvidiaTelemetryReader>();
        services.AddSingleton<AsusStream>();
        services.AddSingleton<ISensorPluginFactory, AsusPluginFactory>();
        return services;
    }
}
