using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Warnings;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class WarningOnlyFactory : ISensorPluginFactory
{
    public string Id => "third-party";
    public string Name => "Third-party test plugin";
    public IWarningSource WarningSource { get; } = new CustomWarningSource();
    public int Created { get; private set; }
    public ISensorPlugin Create(PluginSettings settings)
    {
        Created++;
        throw new InvalidOperationException("Hardware must not open for catalogue discovery.");
    }
}
