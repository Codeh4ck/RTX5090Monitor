using System.Runtime.InteropServices;

namespace Rtx5090Monitoring.App.Platform.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct MinMaxInfo
{
    public NativePoint Reserved;
    public NativePoint MaxSize;
    public NativePoint MaxPosition;
    public NativePoint MinTrackSize;
    public NativePoint MaxTrackSize;
}
