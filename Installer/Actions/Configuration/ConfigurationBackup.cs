using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Rtx5090Monitoring.Installer.Actions.Configuration;

public sealed class ConfigurationBackup
{
    private readonly string _directory;
    private readonly string _configuration;
    private readonly string _backup;

    public ConfigurationBackup(string backupId, string? programData = null)
    {
        if (!Guid.TryParseExact(backupId, "N", out _))
        {
            throw new ArgumentException("Invalid installer backup identifier.", nameof(backupId));
        }

        _directory = Path.Combine(programData ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Rtx5090Monitoring");
        _configuration = Path.Combine(_directory, "appsettings.json");
        _backup = Path.Combine(_directory, "InstallerBackups", backupId);
    }

    public void Capture()
    {
        AssertNoLinks(_directory);
        Directory.CreateDirectory(_directory);

        var parent = Path.GetDirectoryName(_backup)!;
        Directory.CreateDirectory(parent);

        AssertNoLinks(parent);

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);

        foreach (var identity in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
        {
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(identity, null), FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        }

        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is not null)
        {
            security.AddAccessRule(new FileSystemAccessRule(currentUser, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        }

        new DirectoryInfo(parent).SetAccessControl(security);
        if (File.Exists(_configuration))
        {
            File.Copy(_configuration, _backup + ".json", false);
            File.WriteAllText(_backup + ".state", "present");
        }
        else
        {
            File.WriteAllText(_backup + ".state", "absent");
        }
    }

    public void Restore()
    {
        AssertNoLinks(_directory);
        if (!File.Exists(_backup + ".state"))
        {
            return;
        }

        AssertNoLinks(Path.GetDirectoryName(_backup)!);
        if (File.ReadAllText(_backup + ".state") == "present")
        {
            File.Copy(_backup + ".json", _configuration, true);
        }
        else if (File.Exists(_configuration))
        {
            File.Delete(_configuration);
        }

        Clean();
    }

    public void Clean()
    {
        AssertNoLinks(Path.GetDirectoryName(_backup)!);

        File.Delete(_backup + ".json");
        File.Delete(_backup + ".state");
    }

    private static void AssertNoLinks(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Installer configuration paths cannot use symbolic links or junctions.");
            }
        }
    }
}
