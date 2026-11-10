namespace RTXLens.Presentation.Settings;

public sealed record WindowPlacement(double? Width = null, double? Height = null, int? X = null, int? Y = null)
{
    internal bool IsValid =>
        (Width is null && Height is null || Width is >= 1 and <= 7680 && Height is >= 1 and <= 4320)
        && (X is null) == (Y is null);
}

