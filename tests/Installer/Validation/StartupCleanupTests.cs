using System;
using System.Reflection;
using Microsoft.Win32;
using Rtx5090Monitoring.Installer.Actions.Startup;

namespace Rtx5090Monitoring.Installer.Tests.Validation;

internal static class StartupCleanupTests
{
    public static void Run()
    {
        const string parent = @"Software\Rtx5090Monitoring.Installer.Tests\";
        var id = Guid.NewGuid().ToString("N");
        var path = parent + id;
        const string runPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string approvedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        const string directory = @"C:\Program Files\Rtx5090Monitoring";
        using var hive = Registry.CurrentUser.CreateSubKey(path, true);
        try
        {
            using var run = hive.CreateSubKey(runPath, true);
            using var approved = hive.CreateSubKey(approvedPath, true);
            run.SetValue("Rtx5090Monitoring", '"' + directory + "\\Desktop\\Rtx5090Monitoring.App.exe\" --startup");
            run.SetValue("AnotherApplication", "preserve");
            approved.SetValue("Rtx5090Monitoring", new byte[] { 2, 0, 0, 0 });
            Clean(hive, directory);
            Check.That(run.GetValue("Rtx5090Monitoring") is null, "Uninstall removes the installed-path Run entry in an isolated test hive.");
            Check.That(approved.GetValue("Rtx5090Monitoring") is null, "Uninstall removes only its matching StartupApproved entry.");
            Check.That((string)run.GetValue("AnotherApplication") == "preserve", "Uninstall preserves other startup applications.");
            run.SetValue("Rtx5090Monitoring", "\"C:\\Portable\\Rtx5090Monitoring.App.exe\" --startup");
            approved.SetValue("Rtx5090Monitoring", new byte[] { 2, 0, 0, 0 });
            Clean(hive, directory);
            Check.That(run.GetValue("Rtx5090Monitoring") is not null && approved.GetValue("Rtx5090Monitoring") is not null,
                "Portable startup registration and approval are retained when the installed app is removed.");
        }
        finally
        {
            hive.Dispose();
            if (!path.StartsWith(parent, StringComparison.Ordinal) || !Guid.TryParseExact(id, "N", out _))
            {
                throw new InvalidOperationException("Invalid isolated registry cleanup path.");
            }

            Registry.CurrentUser.DeleteSubKeyTree(path, false);
        }
    }

    private static void Clean(RegistryKey hive, string directory) => typeof(StartupCleanup).GetMethod("RemoveFromHive", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [hive, directory]);
}
