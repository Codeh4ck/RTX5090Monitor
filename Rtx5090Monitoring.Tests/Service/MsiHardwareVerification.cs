using System.Text.Json;
using Rtx5090Monitoring.Contracts.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rtx5090Monitoring.Plugins.Msi.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Device;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Service;

internal static class MsiHardwareVerification
{
    public static async Task RunAsync()
    {
        await using var host = await MonitoringTestHost.StartWithMsiHardwareAsync();
        var snapshot = await host.GetSensorSnapshotAsync();
        var sensors = snapshot.GetProperty("sensors").EnumerateArray().ToArray();
        TestAssert.Equal(18, sensors.Length);
        TestAssert.Equal(12, sensors.Count(sensor => sensor.GetProperty("identity").GetProperty("component").GetString() == "PsuPin"));
        TestAssert.Equal(2, sensors.Where(sensor => sensor.GetProperty("identity").GetProperty("component").GetString() == "PsuPin")
            .Select(sensor => sensor.GetProperty("identity").GetProperty("connectorId").GetString()).Distinct().Count());
        TestAssert.True(sensors.All(sensor => sensor.GetProperty("quality").GetString() == "Valid"), "Every published MSI reading must be valid.");
        var device = await host.Client.GetStringAsync("/plugins/msi-psu/device");
        using var json = JsonDocument.Parse(device);
        var model = json.RootElement.GetProperty("model").GetString();
        TestAssert.True(model is "MPG Ai1300TS" or "MPG Ai1600TS", "Only supported TS models may publish telemetry.");
        TestAssert.Equal(2, json.RootElement.GetProperty("pinCurrentsA").GetArrayLength());
        var directory = Path.Combine(Environment.CurrentDirectory, "Rtx5090Monitoring", "artifacts", "msi-service-verification");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "sensors.json"), snapshot.GetRawText());
        await File.WriteAllTextAsync(Path.Combine(directory, "device.json"), device);
        Console.WriteLine($"PASS: Live {model} service publishes 18 typed readings over WebSocket, including 12 distinct connector/pin identities.");
        Console.WriteLine(device);

        var manual = MsiSettings.WithOptions(new PluginSettings(), new MsiOptions { FanControl = MsiFanControl.Manual, FanDutyPercent = 50 });
        await host.SetPluginSettingsAsync("msi-psu", manual);
        await TestWait.UntilAsync(async () =>
        {
            using var response = await host.Client.GetAsync("/plugins/msi-psu/device");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var latest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return latest.RootElement.GetProperty("fanDutyPercent").GetInt32() == 50
                && !latest.RootElement.GetProperty("automaticFan").GetBoolean()
                && latest.RootElement.GetProperty("fanRpm").GetDouble() > 0;
        }, "live fan control readback");
        var manualFrame = await host.GetSensorSnapshotAsync();
        await File.WriteAllTextAsync(Path.Combine(directory, "manual-sensors.json"), manualFrame.GetRawText());
        await host.SetPluginSettingsAsync("msi-psu", manual with { Enabled = false });
        using var reader = new MsiPsuReader(NullLogger<MsiPsuReader>.Instance);
        var restored = reader.Read(new MsiOptions(), DateTimeOffset.UtcNow, CancellationToken.None)!;
        TestAssert.True(restored is { AutomaticFan: true, FanDutyPercent: 0 }, "Disabling the real plugin must restore automatic control.");
        Console.WriteLine("PASS: Live configuration changed actual fan mode/duty; disabling the plugin restored automatic mode and zero requested duty.");
    }
}
