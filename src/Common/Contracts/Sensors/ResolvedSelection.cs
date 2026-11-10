using RTXLens.Contracts.Enums;

namespace RTXLens.Contracts.Sensors;

public sealed record ResolvedSelection(string Name, SelectionState State, string? SensorKey);