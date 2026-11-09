using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Msi.Afterburner;

public sealed record SafeguardHistoryRow(Guid Id, string Name, string Date, string Status, bool Running)
{
    public StatusTone Tone => Status switch
    {
        "Shutdown requested" or "Simulated" => StatusTone.Positive,
        "Shutdown request failed" or "Script verification failed" or "Interrupted" => StatusTone.Negative,
        _ => StatusTone.Caution
    };
}