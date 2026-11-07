using System.Runtime.InteropServices;
using System.Text;

namespace Rtx5090Monitoring.Hardware.Nvidia;

internal sealed class NvidiaIdentityReader : IDisposable
{
    private nint _library;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Initialize();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Enumerate([Out, MarshalAs(UnmanagedType.LPArray, SizeConst = 64)] nint[] handles, out uint count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetNumber(nint handle, out uint value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetPciIds(nint handle, out uint deviceId, out uint subsystemId, out uint revisionId, out uint extendedId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetName(nint handle, [Out] StringBuilder name);

    public Dictionary<string, string> DiscoverDesktopIds()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!OperatingSystem.IsWindows() || (_library == 0 && !NativeLibrary.TryLoad(Path.Combine(Environment.SystemDirectory, "nvapi64.dll"), out _library)))
        {
            return result;
        }

        var query = Marshal.GetDelegateForFunctionPointer<QueryInterface>(NativeLibrary.GetExport(_library, "nvapi_QueryInterface"));
        var initialize = Get<Initialize>(0x0150E828);
        var enumerate = Get<Enumerate>(0xE5AC921F);
        var systemType = Get<GetNumber>(0xBAAABFCC);
        var bus = Get<GetNumber>(0x1BE0B8E5);
        var slot = Get<GetNumber>(0x2A0A350F);
        var pci = Get<GetPciIds>(0x2DDFB66E);
        var fullName = Get<GetName>(0xCEEE8E9F);

        if (initialize is null || initialize() != 0 || enumerate is null || systemType is null || bus is null || slot is null || pci is null || fullName is null)
        {
            return result;
        }

        var handles = new nint[64];
        if (enumerate(handles, out var count) != 0)
        {
            return result;
        }

        for (var index = 0; index < Math.Min(count, handles.Length); index++)
        {
            var handle = handles[index];
            var name = new StringBuilder(64);
            if (fullName(handle, name) != 0 || !name.ToString().Contains("GeForce RTX", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (systemType(handle, out var type) != 0 || type != 2
                || bus(handle, out var busId) != 0 || slot(handle, out var slotId) != 0
                || pci(handle, out var device, out var subsystem, out _, out _) != 0)
            {
                continue;
            }

            result[$"/gpu-nvidia/{index}"] = $"nvidia-pci/{busId:x2}/{slotId:x2}/{device:x8}/{subsystem:x8}";
        }

        return result;

        T? Get<T>(uint id) where T : Delegate
        {
            var address = query(id);
            return address == 0 ? null : Marshal.GetDelegateForFunctionPointer<T>(address);
        }
    }

    public void Dispose()
    {
        if (_library != 0)
        {
            NativeLibrary.Free(_library);
            _library = 0;
        }
    }
}
