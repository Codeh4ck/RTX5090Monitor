using Rtx5090Monitoring.Contracts.Enums;

namespace Rtx5090Monitoring.Contracts.Sensors;

public sealed record ResolvedSelection(string Name, SelectionState State, string? SensorKey);