using Microsoft.Extensions.DependencyInjection;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Msi.Factories;
using Rtx5090Monitoring.Plugins.Msi.Serialization;

namespace Rtx5090Monitoring.Plugins.Msi;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddMsiPlugin(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, MsiJsonContext.Default));
        services.AddSingleton<ISensorPluginFactory, MsiPluginFactory>();
        return services;
    }
}
