using HidSharp;
using Microsoft.Extensions.Logging;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Telemetry;
using Rtx5090Monitoring.Plugins.Msi.Protocol;

namespace Rtx5090Monitoring.Plugins.Msi.Device;

public sealed class MsiPsuReader(ILogger<MsiPsuReader> logger) : IMsiPsuReader
{
    private MsiPsuTransport? _transport;
    private string? _deviceId;
    private string _model = "";
    private string _revision = "";
    private bool _ownsFan;
    private MsiFanControl _appliedControl;
    private int _appliedDuty;

    public MsiDeviceInfo[] Devices => OperatingSystem.IsWindows()
        ?
        [
            .. SupportedDevices()
                .Select(device => new MsiDeviceInfo(device.DevicePath, "MSI " + MsiPsuModels.Name(device.VendorID, device.ProductID)))
        ]
        : [];

    private static HidDevice[] SupportedDevices() =>
    [
        .. DeviceList.Local.GetHidDevices(MsiPsuModels.VendorId)
            .Where(device => MsiPsuModels.Name(device.VendorID, device.ProductID) is not null)
    ];

    public MsiSnapshot? Read(MsiOptions options, DateTimeOffset observedAt, CancellationToken cancellationToken)
    {
        options.Validate();
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var devices = SupportedDevices();
        var device = options.DevicePath is { } path ? devices.SingleOrDefault(item => item.DevicePath == path)
            : devices.Length == 1 ? devices[0] : null;
        if (device is null)
        {
            Disconnect();
            return null;
        }

        try
        {
            if (_deviceId != device.DevicePath || _transport is null)
            {
                Disconnect();

                _transport = new MsiPsuTransport(device);

                ReadPacket(MsiProtocol.Handshake(), 1, cancellationToken);
                _model = MsiProtocol.Text(ReadRegister(0x11, 1, cancellationToken));
                _revision = MsiProtocol.Text(ReadRegister(0x12, 1, cancellationToken));

                if (!MsiPsuModels.Matches(device.ProductID, _model))
                {
                    throw new IOException("The connected PSU model does not match a supported MSI MPG TS device.");
                }

                _deviceId = device.DevicePath;
            }

            ApplyFan(options, cancellationToken);

            var telemetry = ReadRegister(0xE0, 30, cancellationToken);
            var temperature = MsiProtocol.Linear11(ReadRegister(0x30, 2, cancellationToken), 3);
            var rpm = MsiProtocol.Linear11(ReadRegister(0x40, 2, cancellationToken), 3);
            var mode = ReadRegister(0x41, 1, cancellationToken)[3];
            var duty = ReadRegister(0x42, 4, cancellationToken)[3];

            if (mode is not (1 or 3) || duty > 100)
            {
                throw new IOException("Unknown MSI fan mode or duty response.");
            }

            if (options.FanControl != MsiFanControl.Unmanaged && (mode != (options.FanControl == MsiFanControl.Automatic ? 1 : 3) ||
                                                                  (options.FanControl == MsiFanControl.Manual && duty != options.FanDutyPercent)))
            {
                _appliedControl = MsiFanControl.Unmanaged;
                throw new IOException("MSI fan readback differs from configured control; another controller may be active.");
            }

            return new MsiSnapshot(_deviceId!, _model, _revision, observedAt, MsiProtocol.Currents(telemetry), temperature, rpm, mode == 1, duty);
        }
        catch
        {
            Disconnect();
            throw;
        }
    }

    private void ApplyFan(MsiOptions options, CancellationToken token)
    {
        if (options.FanControl == MsiFanControl.Unmanaged)
        {
            ReleaseFanControl();
            return;
        }

        if (_appliedControl == options.FanControl && (options.FanControl != MsiFanControl.Manual || _appliedDuty == options.FanDutyPercent))
        {
            return;
        }
        _ownsFan = true;

        ReadPacket(MsiProtocol.FanRequest(options.FanControl == MsiFanControl.Automatic, options.FanDutyPercent), 1, token);

        _appliedControl = options.FanControl;
        _appliedDuty = options.FanDutyPercent;
    }

    public void ReleaseFanControl()
    {
        if (!_ownsFan || _transport is null)
        {
            return;
        }

        ReadPacket(MsiProtocol.FanRequest(true, 0), 1, CancellationToken.None);
        if (ReadRegister(0x41, 1, CancellationToken.None)[3] != 1 || ReadRegister(0x42, 4, CancellationToken.None)[3] != 0)
        {
            throw new IOException("MSI automatic fan restoration could not be verified.");
        }

        _ownsFan = false;
        _appliedControl = MsiFanControl.Unmanaged;
    }

    private byte[] ReadRegister(byte register, int length, CancellationToken token) => ReadPacket(MsiProtocol.ReadRequest(register), length, token);
    private byte[] ReadPacket(byte[] request, int length, CancellationToken token) => (_transport ?? throw new IOException("MSI PSU disconnected.")).Exchange(request, length, token);

    private void Disconnect()
    {
        try
        {
            ReleaseFanControl();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not restore automatic MSI PSU fan control");
        }
        finally
        {
            _transport?.Dispose();
            _transport = null;
            _deviceId = null;
            _ownsFan = false;
            _appliedControl = MsiFanControl.Unmanaged;
        }
    }

    public void Dispose() => Disconnect();
}
