using RTXLens.Contracts.Configuration;
using RTXLens.Contracts.Sensors;
using RTXLens.Contracts.Warnings;
using RTXLens.Warnings;

namespace RTXLens.Tests.TestDoubles;

internal sealed class CustomWarningSource : IWarningSource
{
    public string PluginId => "third-party";
    public IReadOnlyList<WarningDefinition> Definitions => [new("third-party/condition", PluginId, "Custom condition", "temperature")];
    public bool Registered { get; set; } = true;
    public WarningObservation[] Observe(SensorSnapshot snapshot, PluginSettings settings) =>
        [new(PluginId, "device", Registered ? "third-party/condition" : "third-party/undeclared", true, true, "Custom provider detail")];
}
