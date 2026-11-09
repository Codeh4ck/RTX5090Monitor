using System.Text;
using System.Text.RegularExpressions;

namespace Rtx5090Monitoring.Presentation.Msi.Afterburner;

// A lossless editor: untouched lines and all original terminators round-trip byte for byte.
public sealed partial class IniDocument
{
    private readonly Encoding _encoding;
    private readonly byte[] _preamble;
    private readonly List<Line> _lines;
    private readonly string _newline;

    private sealed record Line(string Text, string Ending);

    public string EncodingName { get; }
    public string Text => string.Concat(_lines.Select(line => line.Text + line.Ending));

    public IniDocument(byte[] bytes)
    {
        (_encoding, _preamble, EncodingName) = DecodeEncoding(bytes);
        var text = _encoding.GetString(bytes, _preamble.Length, bytes.Length - _preamble.Length);
        _lines = SplitLines().Matches(text).Where(match => match.Length > 0)
            .Select(match => new Line(match.Groups[1].Value, match.Groups[2].Value)).ToList();
        _newline = _lines.FirstOrDefault(line => line.Ending.Length > 0)?.Ending ?? "\r\n";

        ValidateStructure();
    }

    public static (string Text, string EncodingName) Preview(byte[] bytes)
    {
        var (encoding, preamble, name) = DecodeEncoding(bytes);
        try
        {
            return (encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length), name);
        }
        catch (DecoderFallbackException)
        {
            return (Encoding.Latin1.GetString(bytes), "Damaged content (original bytes preserved in backup)");
        }
    }

    public bool HasSection(string section) => Locate(section, "").Section >= 0;

    public string? Get(string section, string key)
    {
        var location = Locate(section, key);
        if (location.Key < 0)
        {
            return null;
        }

        var match = KeyLine().Match(_lines[location.Key].Text);
        return match.Groups[3].Value.Trim();
    }

    public void Set(string section, string key, string value)
    {
        if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new ArgumentException("INI values must fit on one line.");
        }

        var location = Locate(section, key);
        if (location.Key >= 0)
        {
            var line = _lines[location.Key];
            var match = KeyLine().Match(line.Text);
            var current = match.Groups[3].Value;
            var leading = current[..(current.Length - current.TrimStart().Length)];
            var trailing = current.Trim().Length == 0 ? "" : current[current.TrimEnd().Length..];

            if (current.Trim() == value)
            {
                return;
            }

            _lines[location.Key] = line with { Text = match.Groups[1].Value + "=" + leading + value + trailing };
            return;
        }

        if (location.Section < 0)
        {
            Append(new Line("[" + section + "]", _newline));
            Append(new Line(key + "=" + value, _newline));
            return;
        }

        var insert = location.Section + 1;
        while (insert < _lines.Count && !SectionLine().IsMatch(_lines[insert].Text))
        {
            insert++;
        }

        if (insert > 0 && _lines[insert - 1].Ending.Length == 0)
        {
            _lines[insert - 1] = _lines[insert - 1] with { Ending = _newline };
        }

        _lines.Insert(insert, new Line(key + "=" + value, _newline));
    }

    public byte[] ToBytes() => [.. _preamble, .. _encoding.GetBytes(Text)];

    private void Append(Line line)
    {
        if (_lines.Count > 0 && _lines[^1].Ending.Length == 0)
        {
            _lines[^1] = _lines[^1] with { Ending = _newline };
        }

        _lines.Add(line);
    }

    private (int Section, int Key) Locate(string section, string key)
    {
        var sectionIndex = -1;
        var keyIndex = -1;
        var inside = false;

        for (var index = 0; index < _lines.Count; index++)
        {
            var heading = SectionLine().Match(_lines[index].Text);
            if (heading.Success)
            {
                inside = heading.Groups[1].Value.Trim().Equals(section, StringComparison.OrdinalIgnoreCase);
                if (inside)
                {
                    if (sectionIndex >= 0)
                    {
                        throw new InvalidDataException("Duplicate INI section: " + section);
                    }

                    sectionIndex = index;
                }

                continue;
            }

            var entry = KeyLine().Match(_lines[index].Text);
            if (inside && entry.Success && entry.Groups[2].Value.Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                if (keyIndex >= 0)
                {
                    throw new InvalidDataException("Duplicate INI key: " + section + "/" + key);
                }

                keyIndex = index;
            }
        }

        return (sectionIndex, keyIndex);
    }

    private void ValidateStructure()
    {
        var hasSection = false;
        foreach (var line in _lines)
        {
            var text = line.Text.Trim();
            if (text.Length == 0 || text.StartsWith(';') || text.StartsWith('#'))
            {
                continue;
            }

            if (text.Contains('\0'))
            {
                throw new InvalidDataException("NUL characters are not supported in an INI document.");
            }

            if (SectionLine().IsMatch(text))
            {
                hasSection = true;
                continue;
            }

            if (!hasSection || !KeyLine().IsMatch(line.Text) || text.Contains('\0'))
            {
                throw new InvalidDataException("Unsupported or malformed INI line. The file was not changed: " + text);
            }
        }
    }

    private static (Encoding, byte[], string) DecodeEncoding(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return (new UTF8Encoding(false, true), [0xEF, 0xBB, 0xBF], "UTF-8 BOM");
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            return (new UnicodeEncoding(false, false, true), [0xFF, 0xFE], "UTF-16 LE");
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            return (new UnicodeEncoding(true, false, true), [0xFE, 0xFF], "UTF-16 BE");
        }

        try
        {
            var utf8 = new UTF8Encoding(false, true);
            _ = utf8.GetString(bytes);
            return (utf8, [], "UTF-8 / ASCII");
        }
        catch (DecoderFallbackException)
        {
            // One-to-one byte mapping keeps legacy ANSI files intact without guessing their code page.
            return (Encoding.Latin1, [], "ANSI (original bytes preserved)");
        }
    }

    [GeneratedRegex(@"([^\r\n]*)(\r\n|\r|\n|$)")]
    private static partial Regex SplitLines();

    [GeneratedRegex(@"^\s*\[([^\]\r\n]+)\]\s*$")]
    private static partial Regex SectionLine();

    [GeneratedRegex(@"^(([^=;#\[\]\r\n]+?)\s*)=(.*)$")]
    private static partial Regex KeyLine();
}