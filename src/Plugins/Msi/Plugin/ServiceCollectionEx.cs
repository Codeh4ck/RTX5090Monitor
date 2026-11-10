using Microsoft.Extensions.DependencyInjection;
using RTXLens.Core.Plugins;
using RTXLens.Plugins.Msi.Factories;
using RTXLens.Plugins.Msi.Serialization;

namespace RTXLens.Plugins.Msi;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddMsiPlugin(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, MsiJsonContext.Default));
        services.AddSingleton<ISensorPluginFactory, MsiPluginFactory>();
        return services;
    }
}
