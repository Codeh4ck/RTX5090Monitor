using RTXLens.Contracts.Configuration;
using RTXLens.Contracts.Discovery;
using RTXLens.Contracts.Sensors;

namespace RTXLens.Debugging.Models;

public sealed record DebugFrame(PluginCatalog Catalog, ConfigurationSnapshot Configuration, SensorSnapshot Snapshot);
