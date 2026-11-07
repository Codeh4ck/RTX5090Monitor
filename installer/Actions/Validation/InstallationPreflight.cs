using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Rtx5090Monitoring.Installer.Actions.Services;

namespace Rtx5090Monitoring.Installer.Actions.Validation;

public static class InstallationPreflight
{
    public static void CheckDesktopClosed(string installDirectory)
    {
        foreach (var process in Process.GetProcessesByName("Rtx5090Monitoring.App"))
        {
            using (process)
            {
                var executable = process.MainModule?.FileName;
                if (executable is not null && IsInstalledApp(executable, installDirectory))
                {
                    throw new IOException("Exit RTX Monitor through its tray menu, then retry. Unsaved configuration drafts will not be discarded automatically.");
                }
            }
        }
    }

    public static bool IsInstalledApp(string executable, string installDirectory) =>
        string.Equals(Path.GetFullPath(executable), Path.Combine(Path.GetFullPath(installDirectory), "Desktop", "Rtx5090Monitoring.App.exe"), StringComparison.OrdinalIgnoreCase);

    public static void CheckServiceOwnership(string installDirectory, bool alreadyInstalled)
    {
        using var registration = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Rtx5090Monitoring");
        var managedDirectory = registration?.GetValue("InstallDirectory") as string;
        var managed = alreadyInstalled || (managedDirectory is not null
                                           && string.Equals(Path.GetFullPath(managedDirectory).TrimEnd(Path.DirectorySeparatorChar),
                                               Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar),
                                               StringComparison.OrdinalIgnoreCase));

        if (!managed && Directory.Exists(installDirectory) && Directory.GetFileSystemEntries(installDirectory).Length > 0)
        {
            throw new IOException("The destination contains files not managed by this installer. Choose an empty dedicated folder or remove the previous unmanaged installation first.");
        }

        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Rtx5090MonitoringService");
        if (key?.GetValue("ImagePath") is not string image)
        {
            return;
        }

        var expected = Path.Combine(Path.GetFullPath(installDirectory), "Service", "Rtx5090Monitoring.Service.exe");
        if (!managed || !image.StartsWith('"' + expected + '"', StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("An unmanaged Rtx5090MonitoringService installation already exists. Remove that service registration before installing this release. Runtime configuration will be preserved.");
        }
    }

    public static void CheckPortAvailable()
    {
        var ownProcess = ServiceStartupConfiguration.RunningProcessId();
        foreach (var process in TcpListenerOwners.Read(59273))
        {
            if (ownProcess == 0 || process != ownProcess)
            {
                throw new IOException($"Port 59273 is already in use by process {process}. Stop the original WireView/ASUS service or other listener, then retry. RTX Monitor will not stop an unrelated service.");
            }
        }
    }
}
