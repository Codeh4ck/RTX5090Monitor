using Rtx5090Monitoring.App.Platform.Interop;

namespace Rtx5090Monitoring.App.Platform.Startup;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsFirst { get; }

    public SingleInstance(bool diagnostic = false)
    {
        _mutex = new Mutex(false, @"Local\Rtx5090Monitoring.App." + Environment.UserName + (diagnostic ? ".SmokeTest" : string.Empty), out var created);
        IsFirst = created;
    }

    public static void ShowExisting()
    {
        var window = NativeMethods.FindWindowW(null, "RTX Monitor");
        if (window != 0)
        {
            var message = NativeMethods.RegisterWindowMessageW("Rtx5090Monitoring.App.Show");
            NativeMethods.PostMessageW(window, message, 0, 0);
        }
    }

    public void Dispose() => _mutex.Dispose();
}
