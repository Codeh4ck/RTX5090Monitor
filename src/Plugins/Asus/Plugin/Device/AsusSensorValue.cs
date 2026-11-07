using Rtx5090Monitoring.Plugins.Asus.Mapping;

namespace Rtx5090Monitoring.Plugins.Asus.Device;

public sealed record AsusSensorValue(AsusSensorDefinition Definition, float? Value);
