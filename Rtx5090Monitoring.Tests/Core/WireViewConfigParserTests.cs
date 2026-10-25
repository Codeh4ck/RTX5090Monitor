using System.Buffers.Binary;
using System.Text;
using Rtx5090Monitoring.Plugins.WireView.Device;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Core;

internal static class WireViewConfigParserTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(ParsesVersionZeroWithoutAveraging);
        runner.Run(ParsesVersionOneAveraging);
        runner.Run(ParsesVersionTwoAndPreservesThresholdsAndMasks);
    }

    private static void ParsesVersionZeroWithoutAveraging()
    {
        var bytes = CreateConfigBytes(64);
        var configuration = WireViewProReader.ParseConfig(bytes, version: 0);

        TestAssert.Equal("Test device", configuration.FriendlyName);
        TestAssert.Equal<string?>(null, configuration.Averaging);
        TestAssert.Equal(0, configuration.Version);
    }

    private static void ParsesVersionOneAveraging()
    {
        var bytes = CreateConfigBytes(72);
        bytes[64] = 5;

        var configuration = WireViewProReader.ParseConfig(bytes, version: 1);

        TestAssert.Equal("709ms", configuration.Averaging);
        TestAssert.Equal(1, configuration.Version);
    }

    private static void ParsesVersionTwoAndPreservesThresholdsAndMasks()
    {
        var bytes = CreateConfigBytes(88);
        bytes[64] = 6;

        var configuration = WireViewProReader.ParseConfig(bytes, version: 2);

        TestAssert.Equal(2, configuration.Version);
        TestAssert.Equal("1417ms", configuration.Averaging);
        TestAssert.Equal("Fixed", configuration.FanMode);
        TestAssert.Equal("Tmax", configuration.FanTempSource);
        TestAssert.Equal(35d, configuration.FanTempMinC);
        TestAssert.Equal(80d, configuration.TsFaultThresholdC);
        TestAssert.Equal(9.2, configuration.WireOcpFaultThresholdA);
        TestAssert.Equal((ushort)600, configuration.OppFaultThresholdW);
        TestAssert.Equal((ushort)0x3E, configuration.FaultDisplayEnable);
        TestAssert.Equal((ushort)0x08, configuration.FaultHardPowerEnable);
        TestAssert.Equal((byte)3, configuration.ShutdownWaitTimeS);
    }

    private static byte[] CreateConfigBytes(int size)
    {
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes("Test device").CopyTo(bytes, 3);
        bytes[36] = 1;
        bytes[37] = 4;
        bytes[38] = 20;
        bytes[39] = 100;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), 350);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 700);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(46), 0x3E);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52), 0x08);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(54), 800);
        bytes[56] = 55;
        bytes[57] = 92;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(58), 600);
        bytes[60] = 30;
        bytes[61] = 6;
        bytes[62] = 3;
        bytes[63] = 1;
        return bytes;
    }
}
