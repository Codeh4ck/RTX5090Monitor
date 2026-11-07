using Rtx5090Monitoring.Contracts.Warnings;

namespace Rtx5090Monitoring.Warnings;

public sealed record ConfiguredWarning(WarningDefinition Definition, WarningObservation Observation, WarningPolicy? Policy);
