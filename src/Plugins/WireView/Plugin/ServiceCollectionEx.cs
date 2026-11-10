using Microsoft.Extensions.DependencyInjection;
using RTXLens.Core.Plugins;
using RTXLens.Plugins.WireView.Factories;
using RTXLens.Plugins.WireView.Serialization;
using RTXLens.Plugins.WireView.Streaming;

namespace RTXLens.Plugins.WireView;

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
