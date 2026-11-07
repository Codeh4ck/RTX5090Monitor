using Rtx5090Monitoring.Contracts.Configuration;

namespace Rtx5090Monitoring.Core.Plugins;

public interface ISensorPlugin : IDisposable
{
    PluginDescriptor Descriptor { get; }
    ValueTask<PluginSample> PollAsync(CancellationToken cancellationToken);
    void ApplySettings(PluginSettings settings);
}
