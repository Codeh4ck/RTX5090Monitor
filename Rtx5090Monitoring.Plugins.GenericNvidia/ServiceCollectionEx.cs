using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Hardware.Nvidia;
using Rtx5090Monitoring.Plugins.GenericNvidia.Serialization;

namespace Rtx5090Monitoring.Plugins.GenericNvidia;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddGenericNvidiaPlugin(this IServiceCollection services)
    {
        services.TryAddSingleton<INvidiaHardwareSource, LibreNvidiaHardwareSource>();
        services.TryAddSingleton<NvidiaTelemetryReader>();
        services.AddSingleton<ISensorPluginFactory, GenericNvidiaPluginFactory>();
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, GenericNvidiaJsonContext.Default));
        return services;
    }
}
