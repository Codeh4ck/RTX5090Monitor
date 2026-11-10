using System.Runtime.InteropServices;

namespace RTXLens.App.Platform.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}
