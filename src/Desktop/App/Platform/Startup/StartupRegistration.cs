using Microsoft.Win32;

namespace Rtx5090Monitoring.App.Platform.Startup;

internal static class StartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Rtx5090Monitoring";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key, true);
        if (!enabled)
        {
            key.DeleteValue(Name, false);
            return;
        }

        var executable = Path.Combine(AppContext.BaseDirectory, "Rtx5090Monitoring.App.exe");
        if (!File.Exists(executable))
        {
            throw new IOException("The desktop executable was not found. Publish the portable app before enabling Windows startup.");
        }

        var command = $"\"{executable}\" --startup";
        if (!string.Equals(key.GetValue(Name) as string, command, StringComparison.Ordinal))
        {
            key.SetValue(Name, command, RegistryValueKind.String);
        }
    }
}
