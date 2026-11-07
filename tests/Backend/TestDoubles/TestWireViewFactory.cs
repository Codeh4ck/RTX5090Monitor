using Rtx5090Monitoring.Plugins.WireView.Analysis.Warnings;
using Rtx5090Monitoring.Warnings;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.WireView;
using Rtx5090Monitoring.Plugins.WireView.Streaming;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class TestWireViewFactory(WireViewTestState state, WireViewStream stream) : ISensorPluginFactory
{
    public IWarningSource WarningSource { get; } = new WireViewWarningSource();
    public string Id => WireViewPlugin.PluginId;
    public string Name => "Test WireView";

    public ISensorPlugin Create(PluginSettings settings)
    {
        if (state.FailCreation)
        {
            throw new InvalidOperationException("Simulated plugin activation failure.");
        }

        Interlocked.Increment(ref state.CreateCount);
        return new WireViewPlugin(settings, TimeProvider.System, new TestWireViewReader(state), stream);
    }
}
