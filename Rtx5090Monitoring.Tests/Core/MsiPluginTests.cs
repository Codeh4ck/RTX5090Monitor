using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Plugins.Msi;
using Rtx5090Monitoring.Plugins.Msi.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Device;
using Rtx5090Monitoring.Plugins.Msi.Protocol;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.Tests.TestDoubles;

namespace Rtx5090Monitoring.Tests.Core;

internal static class MsiPluginTests
{
    public static async Task RunAsync(TestRunner runner)
    {
        runner.Run(DecodesCapturedPinData);
        runner.Run(RejectsForeignBusyAndTruncatedReports);
        runner.Run(LimitsWritesToVerifiedFanCommands);
        runner.Run(ValidatesOptions);
        runner.Run(RecognizesBothTsModelsAndRejectsOtherDevices);
        await runner.RunAsync(PreservesConnectorIdentityAndDropsDisconnectedReadingsAsync);
    }

    private static void DecodesCapturedPinData()
    {
        var raw = Convert.FromHexString("0051E05FF0A9E029E00FE00EE00FE00EE00FE00EE000E000E000E000E000E000E0");
        var reply = MsiProtocol.MatchReply(MsiProtocol.ReadRequest(0xE0), raw, 30)!;
        var currents = MsiProtocol.Currents(reply);
        TestAssert.Equal(0.9375, currents[0][0]);
        TestAssert.Equal(0.875, currents[0][1]);
        TestAssert.True(currents[1].All(value => value == 0), "Unused connector currents must remain zero.");
    }

    private static void RejectsForeignBusyAndTruncatedReports()
    {
        var request = MsiProtocol.ReadRequest(0xE0);
        TestAssert.True(MsiProtocol.MatchReply(request, [0x51, 0x40, 0, 0], 2) is null, "Foreign report must be ignored.");
        TestAssert.Throws<IOException>(() => MsiProtocol.MatchReply(request, [0x51, 0xE0, 1], 30));
        TestAssert.Throws<IOException>(() => MsiProtocol.MatchReply(request, [0x51, 0xE0, 0xFE, 0], 2));
        TestAssert.True(MsiProtocol.MatchReply(request, [0x51, 0xE0, 0xFE, 1], 2) is not null, "Valid FE data is not a busy response.");
    }

    private static void LimitsWritesToVerifiedFanCommands()
    {
        TestAssert.Equal("005041030032", Convert.ToHexString(MsiProtocol.FanRequest(false, 50).AsSpan(0, 6)));
        TestAssert.Equal("005041010000", Convert.ToHexString(MsiProtocol.FanRequest(true, 99).AsSpan(0, 6)));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MsiProtocol.FanRequest(false, 101));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MsiProtocol.ReadRequest(0xC0));
    }

    private static void ValidatesOptions()
    {
        TestAssert.Equal(MsiFanControl.Unmanaged, MsiSettings.GetOptions(new PluginSettings()).FanControl);
        TestAssert.Throws<ArgumentException>(() => MsiSettings.GetOptions(new PluginSettings { PollIntervalMs = 500 }));
        TestAssert.Throws<ArgumentException>(() => new MsiOptions { FanDutyPercent = -1 }.Validate());
        TestAssert.Throws<ArgumentException>(() => new MsiOptions { FanControl = (MsiFanControl)9 }.Validate());
    }

    private static void RecognizesBothTsModelsAndRejectsOtherDevices()
    {
        TestAssert.Equal("MPG Ai1300TS", MsiPsuModels.Name(0x0DB0, 0xAA6F));
        TestAssert.Equal("MPG Ai1600TS", MsiPsuModels.Name(0x0DB0, 0x808C));
        TestAssert.True(MsiPsuModels.Matches(0x808C, "MPG Ai1600TS"), "Ai1600TS must pass model verification.");
        TestAssert.False(MsiPsuModels.Matches(0xAA6F, "MPG Ai1600TS"), "Model and USB identity must agree.");
        TestAssert.False(MsiPsuModels.Matches(0x808C, "MEG Ai1600T"), "Other MSI families must remain excluded.");
        TestAssert.Equal<string?>(null, MsiPsuModels.Name(0xFFFF, 0x808C));
        TestAssert.Equal<string?>(null, MsiPsuModels.Name(0x0DB0, 0xFFFF));
    }

    private static async Task PreservesConnectorIdentityAndDropsDisconnectedReadingsAsync()
    {
        var state = new MsiTestState();
        using var plugin = new MsiPlugin(new PluginSettings(), TimeProvider.System, new TestMsiReader(state));
        var sample = await plugin.PollAsync(CancellationToken.None);
        TestAssert.Equal(18, sample.Sensors.Length);
        TestAssert.Equal(18, sample.Sensors.Select(sensor => sensor.Identity.Key).Distinct().Count());
        TestAssert.Equal(12, sample.Sensors.Count(sensor => sensor.Identity.Component == SensorComponent.PsuPin));
        TestAssert.False(sample.Sensors.Any(sensor => sensor.Identity.Metric is SensorMetric.Power or SensorMetric.Voltage or SensorMetric.FaultStatus),
            "Unverified measurements must never be fabricated.");
        plugin.ApplySettings(MsiSettings.WithOptions(new PluginSettings(), new MsiOptions { FanControl = MsiFanControl.Manual, FanDutyPercent = 75 }));
        await plugin.PollAsync(CancellationToken.None);
        TestAssert.Equal(75, state.LastOptions!.FanDutyPercent);
        state.Available = false;
        var missing = await plugin.PollAsync(CancellationToken.None);
        TestAssert.Equal(PluginState.NoDevice, missing.State);
        TestAssert.True(plugin.Latest is null && missing.Sensors.Length == 0, "Disconnect must clear cached values.");
    }
}
