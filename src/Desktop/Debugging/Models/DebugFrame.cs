using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Sensors;

namespace Rtx5090Monitoring.Debugging.Models;

public sealed record DebugFrame(PluginCatalog Catalog, ConfigurationSnapshot Configuration, SensorSnapshot Snapshot);
