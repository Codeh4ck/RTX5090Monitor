using RTXLens.Contracts.Warnings;

namespace RTXLens.Warnings;

public sealed record ConfiguredWarning(WarningDefinition Definition, WarningObservation Observation, WarningPolicy? Policy);
