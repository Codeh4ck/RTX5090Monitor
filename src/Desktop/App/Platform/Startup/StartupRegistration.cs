using System.Security.Principal;
using Microsoft.Win32;
using Rtx5090Monitoring.Installer.Actions.Startup;

namespace Rtx5090Monitoring.App.Platform.Startup;

internal static class StartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Rtx5090Monitoring";

    public static void SetEnabled(bool enabled)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User?.Value ?? throw new IOException("The Windows account could not be identified.");
        var executable = Path.Combine(AppContext.BaseDirectory, "Rtx5090Monitoring.App.exe");
        if (enabled)
        {
            ElevatedStartupTask.Register(sid, executable);
        }
        else
        {
            ElevatedStartupTask.Remove(sid, executable);
        }

        using var key = Registry.CurrentUser.OpenSubKey(Key, writable: true);
        if (key?.GetValue(Name) is string command
            && (string.Equals(command, $"\"{executable}\"", StringComparison.OrdinalIgnoreCase)
                || command.StartsWith($"\"{executable}\" ", StringComparison.OrdinalIgnoreCase)))
        {
            key.DeleteValue(Name, throwOnMissingValue: false);
        }
    }
}