using Microsoft.Extensions.DependencyInjection;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.WireView.Factories;
using Rtx5090Monitoring.Plugins.WireView.Serialization;
using Rtx5090Monitoring.Plugins.WireView.Streaming;

namespace Rtx5090Monitoring.Plugins.WireView;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddWireViewPlugin(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, WireViewJsonContext.Default));
        services.AddSingleton<WireViewStream>();
        services.AddSingleton<ISensorPluginFactory, WireViewPluginFactory>();
        return services;
    }
}
