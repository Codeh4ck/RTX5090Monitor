using System;
using System.IO;
using Rtx5090Monitoring.Installer.Actions.Configuration;

namespace Rtx5090Monitoring.Installer.Tests.Configuration;

internal static class ConfigurationBackupTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rtx-installer-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var configuration = Path.Combine(directory, "Rtx5090Monitoring", "appsettings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configuration)!);
            File.WriteAllText(configuration, "{\"original\":true}");
            var backup = new ConfigurationBackup(Guid.NewGuid().ToString("N"), directory);
            backup.Capture();
            File.WriteAllText(configuration, "{\"migrated\":true}");
            backup.Restore();
            Check.That(File.ReadAllText(configuration) == "{\"original\":true}", "Rollback restores the exact previous configuration.");

            File.Delete(configuration);
            backup = new ConfigurationBackup(Guid.NewGuid().ToString("N"), directory);
            backup.Capture();
            File.WriteAllText(configuration, "{\"seeded\":true}");
            backup.Restore();
            Check.That(!File.Exists(configuration), "Failed fresh installation removes only its newly seeded configuration.");

            File.WriteAllText(configuration, "{\"preserved\":true}");
            backup = new ConfigurationBackup(Guid.NewGuid().ToString("N"), directory);
            backup.Capture();
            backup.Clean();
            Check.That(File.Exists(configuration), "Successful installation backup cleanup preserves live configuration.");
            backup = new ConfigurationBackup(Guid.NewGuid().ToString("N"), directory);
            backup.Restore();
            Check.That(File.Exists(configuration), "An unscheduled or failed snapshot cannot erase configuration during rollback.");
        }
        finally
        {
            var target = Path.GetFullPath(directory);
            var root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(target).StartsWith("rtx-installer-test-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Invalid test cleanup path.");
            }

            Directory.Delete(target, true);
        }
    }
}
