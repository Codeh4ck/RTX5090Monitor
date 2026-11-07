namespace Rtx5090Monitoring.Presentation.Configuration;

public static class NumericInput
{
    public static int Integer(double value, string label)
    {
        if (!double.IsFinite(value) || Math.Abs(value - Math.Truncate(value)) > 0.0001 || value < int.MinValue || value > int.MaxValue)
        {
            throw new ArgumentException($"{label} must be a whole number.");
        }

        return (int)value;
    }
}