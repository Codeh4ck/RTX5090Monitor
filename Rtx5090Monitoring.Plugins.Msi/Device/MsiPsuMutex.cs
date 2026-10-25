using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Rtx5090Monitoring.Plugins.Msi.Device;

internal sealed class MsiPsuMutex : IDisposable
{
    private readonly nint _handle;

    public MsiPsuMutex()
    {
        _handle = MsiNativeMethods.OpenMutex(0x00100000, false, @"Global\MSI_PSU_Mutex");
        if (_handle != 0)
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error != 2)
        {
            throw new Win32Exception(error, "Cannot open MSI's PSU access mutex.");
        }

        _handle = MsiNativeMethods.CreateMutex(0, false, @"Global\MSI_PSU_Mutex");
        if (_handle == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Acquire()
    {
        var result = MsiNativeMethods.WaitForSingleObject(_handle, 2000);
        switch (result)
        {
            case 0 or 0x80:
                return;
            case 0x102:
                throw new TimeoutException("MSI PSU mutex was held by another application.");
            default:
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Release()
    {
        if (!MsiNativeMethods.ReleaseMutex(_handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to release MSI PSU mutex.");
        }
    }

    public void Dispose() => MsiNativeMethods.CloseHandle(_handle);
}