using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using RTXLens.Installer.Actions.Configuration;

namespace RTXLens.Installer.Actions.Validation;

internal static class AfterburnerShutdown
{
    private const uint CloseMessage = 0x0010;
    private const int ExitTimeoutMs = 10_000;
    private const string CloseManually = "Exit MSI Afterburner from its tray icon before retrying Setup.";

    internal static bool IsRunning()
    {
        var processes = Process.GetProcessesByName("MSIAfterburner");
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length != 0;
    }

    internal static void Close(string executable, Action<string> log) =>
        WithMatchingProcesses(executable, process =>
        {
            log($"Requesting a normal MSI Afterburner exit (PID {process.Id}).");

            var window = FindWindow(process);
            if (window == IntPtr.Zero || !PostMessageW(window, CloseMessage, IntPtr.Zero, IntPtr.Zero))
            {
                throw new IOException(CloseManually);
            }

            if (!process.WaitForExit(ExitTimeoutMs))
            {
                throw new IOException("MSI Afterburner did not exit in time. " + CloseManually);
            }
        });

    internal static void RequireStopped(string executable) =>
        WithMatchingProcesses(executable, _ => throw new IOException(CloseManually));

    private static void WithMatchingProcesses(string executable, Action<Process> action)
    {
        var processes = Process.GetProcessesByName("MSIAfterburner");
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    var imagePath = process.MainModule?.FileName;
                    if (imagePath is not null && LegacyInstallationMigration.SamePath(imagePath, executable))
                    {
                        action(process);
                    }
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The process exited between enumeration and inspection.
                }
                catch (Win32Exception) when (process.HasExited)
                {
                    // The process exited before Windows could resolve its image path.
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static IntPtr FindWindow(Process process)
    {
        using var current = Process.GetCurrentProcess();
        if (process.SessionId != current.SessionId)
        {
            return IntPtr.Zero;
        }

        var window = process.MainWindowHandle;
        if (window != IntPtr.Zero)
        {
            return window;
        }

        // Process.MainWindowHandle skips hidden tray windows.
        var title = new StringBuilder(256);
        EnumWindows((candidate, _) =>
        {
            GetWindowThreadProcessId(candidate, out var owner);
            if (owner != (uint)process.Id)
            {
                return true;
            }

            GetWindowTextW(candidate, title, title.Capacity);
            if (!title.ToString().StartsWith("MSI Afterburner", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            window = candidate;
            return false;
        }, IntPtr.Zero);

        return window;
    }

    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
