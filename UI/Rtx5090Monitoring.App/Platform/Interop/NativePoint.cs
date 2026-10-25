using System.Runtime.InteropServices;

namespace Rtx5090Monitoring.App.Platform.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}
