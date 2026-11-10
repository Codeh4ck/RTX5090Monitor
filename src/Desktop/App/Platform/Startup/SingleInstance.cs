using RTXLens.App.Platform.Interop;

namespace RTXLens.App.Platform.Startup;

internal sealed partial class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsFirst { get; }

    public SingleInstance(bool diagnostic = false)
    {
        _mutex = new Mutex(false, @"Local\RTXLens.App." + Environment.UserName + (diagnostic ? ".SmokeTest" : string.Empty), out var created);
        IsFirst = created;
    }

    public static void ShowExisting()
    {
        var window = NativeMethods.FindWindowW(null, "RTX Lens");
        if (window != 0)
        {
            var message = NativeMethods.RegisterWindowMessageW("RTXLens.App.Show");
            NativeMethods.PostMessageW(window, message, 0, 0);
        }
    }

    public void Dispose() => _mutex.Dispose();
}
