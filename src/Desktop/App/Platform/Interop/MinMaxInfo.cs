using System.Runtime.InteropServices;

namespace RTXLens.App.Platform.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct MinMaxInfo
{
    public NativePoint Reserved;
    public NativePoint MaxSize;
    public NativePoint MaxPosition;
    public NativePoint MinTrackSize;
    public NativePoint MaxTrackSize;
}
