using Rtx5090Monitoring.Plugins.Asus.Analysis.Warnings;
using Rtx5090Monitoring.Warnings;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Asus;
using Rtx5090Monitoring.Plugins.Asus.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Serialization;
using Rtx5090Monitoring.Plugins.Asus.Streaming;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class TestAsusFactory(AsusTestState state, AsusStream stream) : ISensorPluginFactory
{
    public IWarningSource WarningSource { get; } = new AsusWarningSource();
    public string Id => AsusPlugin.PluginId;
    public string Name => "Test ASUS GPU";

    public PluginSettings CreateDefaultSettings() => AsusSettings.WithOptions(new PluginSettings(), new AsusOptions());
    public void ValidateSettings(PluginSettings settings) => AsusSettings.GetOptions(settings);
    public JsonElement ReadBootstrapOptions(IConfigurationSection section) =>
        JsonSerializer.SerializeToElement(section.Get<AsusOptions>(binding => binding.ErrorOnUnknownConfiguration = true)
            ?? new AsusOptions(), AsusJsonContext.Default.AsusOptions);

    public ISensorPlugin Create(PluginSettings settings)
    {
        Interlocked.Increment(ref state.CreateCount);
        return new AsusPlugin(settings, TimeProvider.System, new TestAsusReader(state), stream);
    }
}
