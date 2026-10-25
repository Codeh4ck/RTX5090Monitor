using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Warnings;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class CustomWarningSource : IWarningSource
{
    public string PluginId => "third-party";
    public IReadOnlyList<WarningDefinition> Definitions => [new("third-party/condition", PluginId, "Custom condition", "temperature")];
    public bool Registered { get; set; } = true;
    public WarningObservation[] Observe(SensorSnapshot snapshot, PluginSettings settings) =>
        [new(PluginId, "device", Registered ? "third-party/condition" : "third-party/undeclared", true, true, "Custom provider detail")];
}
