using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Msi;
using Rtx5090Monitoring.Plugins.Msi.Configuration;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class TestMsiFactory(MsiTestState state) : ISensorPluginFactory
{
    public string Id => MsiPlugin.PluginId;
    public string Name => "MSI MPG Ai1300TS";
    public void ValidateSettings(PluginSettings settings) => MsiSettings.GetOptions(settings);
    public ISensorPlugin Create(PluginSettings settings) => new MsiPlugin(settings, TimeProvider.System, new TestMsiReader(state));
}
