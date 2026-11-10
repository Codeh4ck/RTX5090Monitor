using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace RTXLens.Installer.Actions.Configuration;

internal sealed class LegacyInstallationMigration(string previous, string destination, string transactionId)
{
    private static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    private readonly string _journal = Guid.TryParseExact(transactionId, "N", out _)
        ? Path.Combine(ProgramData, "RTXLens.Upgrade." + transactionId)
        : throw new ArgumentException("Invalid upgrade transaction identifier.");
    private readonly LegacySafeguardMigration _safeguard = new(previous, destination, ProgramData);

    private (string Old, string New)[] Directories =>
    [
        (Path.Combine(ProgramData, "Rtx5090Monitoring"), Path.Combine(ProgramData, "RTXLens")),
        (Path.Combine(ProgramData, "Rtx5090Monitoring.Safeguard"), Path.Combine(ProgramData, "RTXLens.Safeguard")),
        (Path.Combine(ProgramData, "Rtx5090Monitoring.AfterburnerBackups"), Path.Combine(ProgramData, "RTXLens.AfterburnerBackups")),
        (Path.Combine(previous, "Desktop", "config"), Path.Combine(destination, "Desktop", "config")),
        (Path.Combine(previous, "Desktop", "AfterburnerBackups"), Path.Combine(destination, "Desktop", "AfterburnerBackups"))
    ];

    public void Capture()
    {
        AssertNoLinks(previous);
        AssertNoLinks(destination);
        AssertNoLinks(_journal);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        foreach (var identity in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
        {
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(identity, null), FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        }

        if (Directory.Exists(_journal))
        {
            throw new IOException("The upgrade transaction directory already exists.");
        }

        Directory.CreateDirectory(_journal, security);
        _safeguard.Capture(_journal);
    }

    public void Apply()
    {
        AssertNoLinks(_journal);
        var directories = Directories;
        for (var index = 0; index < directories.Length; index++)
        {
            var (source, target) = directories[index];
            if (SamePath(source, target) || !Directory.Exists(source))
            {
                continue;
            }

            AssertNoLinks(source);
            AssertNoLinks(target);
            if (Directory.Exists(target) || File.Exists(target))
            {
                throw new IOException("Setup found data in both the RTX Monitor and RTX Lens locations. Preserve the data in " + target + " before retrying.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(MoveMarker(index), "");
            Directory.Move(source, target);
        }

        _safeguard.Apply(_journal);
    }

    public void Restore()
    {
        AssertNoLinks(_journal);
        if (!Directory.Exists(_journal))
        {
            return;
        }

        _safeguard.Restore(_journal);

        var directories = Directories;
        for (var index = directories.Length - 1; index >= 0; index--)
        {
            var (source, target) = directories[index];
            if (!File.Exists(MoveMarker(index)) || Directory.Exists(source) || !Directory.Exists(target))
            {
                continue;
            }

            AssertNoLinks(source);
            AssertNoLinks(target);
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            Directory.Move(target, source);
        }

        Clean();
    }

    public void Clean()
    {
        AssertNoLinks(_journal);
        if (!Directory.Exists(_journal))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_journal))
        {
            AssertNoLinks(file);
            File.Delete(file);
        }

        Directory.Delete(_journal);
    }

    private string MoveMarker(int index) => Path.Combine(_journal, "move-" + index);

    internal static bool SamePath(string first, string second) =>
        string.Equals(Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    internal static void AssertNoLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("An upgrade path contains a symbolic link or junction: " + current);
            }
        }
    }
}
