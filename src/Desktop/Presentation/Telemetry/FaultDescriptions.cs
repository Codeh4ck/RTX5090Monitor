namespace Rtx5090Monitoring.Presentation.Telemetry;

public static class FaultDescriptions
{
    public static string Describe(ushort flags)
    {
        if (flags == 0)
        {
            return "Clear";
        }

        var names = new List<string>();
        if ((flags & 0x02) != 0)
        {
            names.Add("Overtemperature");
        }

        if ((flags & 0x04) != 0)
        {
            names.Add("Overcurrent");
        }

        if ((flags & 0x08) != 0)
        {
            names.Add("Wire overcurrent");
        }

        if ((flags & 0x10) != 0)
        {
            names.Add("Overpower");
        }

        if ((flags & 0x20) != 0)
        {
            names.Add("Current imbalance");
        }

        var unknown = flags & ~0x3E;
        if (unknown != 0)
        {
            names.Add("Unrecognized device fault");
        }

        return string.Join(", ", names);
    }
}
