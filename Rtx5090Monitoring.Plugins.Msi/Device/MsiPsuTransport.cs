using System.Diagnostics;
using HidSharp;
using Rtx5090Monitoring.Plugins.Msi.Protocol;

namespace Rtx5090Monitoring.Plugins.Msi.Device;

internal sealed class MsiPsuTransport : IDisposable
{
    private readonly HidStream _stream;
    private readonly MsiPsuMutex _mutex;

    public MsiPsuTransport(HidDevice device)
    {
        _mutex = new MsiPsuMutex();
        try
        {
            if (device.GetMaxInputReportLength() != 65 || device.GetMaxOutputReportLength() != 65)
            {
                throw new IOException("Unsupported MSI HID report layout.");
            }

            _stream = device.Open();
            _stream.WriteTimeout = 500;
        }
        catch
        {
            _mutex.Dispose();
            throw;
        }
    }

    public byte[] Exchange(byte[] packet, int minimumPayload, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _mutex.Acquire();
        try
        {
            _stream.ReadTimeout = 1;
            var drained = false;
            for (var count = 0; count < 64; count++)
            {
                try { _stream.Read(); }
                catch (TimeoutException) { drained = true; break; }
            }
            if (!drained)
            {
                throw new IOException("PSU input queue is busy; skipping this transaction.");
            }

            token.ThrowIfCancellationRequested();
            _stream.Write(packet);
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 500)
            {
                token.ThrowIfCancellationRequested();
                _stream.ReadTimeout = 100;
                byte[] raw;
                try { raw = _stream.Read(); }
                catch (TimeoutException) { continue; }
                if (MsiProtocol.MatchReply(packet, raw, minimumPayload) is { } reply)
                {
                    return reply;
                }
            }
            throw new TimeoutException("No matching MSI PSU reply within 500 ms.");
        }
        finally { _mutex.Release(); }
    }

    public void Dispose()
    {
        _stream.Dispose();
        _mutex.Dispose();
    }
}
