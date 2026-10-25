namespace Rtx5090Monitoring.Presentation.Telemetry;

internal static class WireViewFaultMetrics
{
    public static MetricViewModel[] Create(ushort status, ushort log)
    {
        var metrics = new List<MetricViewModel>();
        Add(0x02, "Overtemperature");
        Add(0x04, "Overcurrent");
        Add(0x08, "Wire overcurrent");
        Add(0x10, "Overpower");
        Add(0x20, "Current imbalance");
        Add(unchecked((ushort)~0x3E), "Unrecognized device fault");
        return [.. metrics];

        void Add(ushort mask, string name)
        {
            var active = (status & mask) != 0;
            var logged = (log & mask) != 0;
            if (active || logged)
            {
                metrics.Add(new MetricViewModel(name, active && logged ? "Active / Logged" : active ? "Active" : "Logged", "overcurrent") { IsWarning = true });
            }
        }
    }
}
