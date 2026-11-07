using System.Globalization;

namespace Rtx5090Monitoring.Contracts.Updates;

public readonly record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? text, out SemanticVersion version)
    {
        version = default;
        var parts = text?.Split('.');

        if (parts is not { Length: 3 })
        {
            return false;
        }

        var values = new int[3];

        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];

            if (part.Length == 0 || part.Length > 1 && part[0] == '0'
                || part.Any(character => character is < '0' or > '9')
                || !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out values[index]))
            {
                return false;
            }
        }

        version = new SemanticVersion(values[0], values[1], values[2]);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var major = Major.CompareTo(other.Major);
        var minor = Minor.CompareTo(other.Minor);

        return major != 0 ? major : minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
