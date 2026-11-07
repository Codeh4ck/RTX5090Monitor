using System.Runtime.InteropServices;

namespace Rtx5090Monitoring.App.Platform.Interop;

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam);
