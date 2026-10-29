using LibreHardwareMonitor.Hardware;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Hardware.Nvidia;
using Rtx5090Monitoring.Plugins.GenericNvidia;
using Rtx5090Monitoring.Plugins.GenericNvidia.Configuration;
using Rtx5090Monitoring.Plugins.GenericNvidia.Mapping;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.Tests.TestDoubles;

namespace Rtx5090Monitoring.Tests.Core;

internal static class GenericNvidiaTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(SharesOneAcquisitionAndRetainsTheOriginalObservationTime);
        runner.Run(PreservesSelectedIdentityWhenEnumerationChanges);
        runner.Run(MapsSingleAndMultipleFansByCoolerIdWithoutInventingDuty);
        runner.Run(MapsCoreVoltageAndHotspotWithoutPublishingBoardPower);
        runner.Run(InputVoltageUsesMeasuredPinsAndNeverPowerDividedByCurrent);
        runner.Run(MigrationPreservesExistingThresholdsAndDefaults);
        runner.Run(LegacyThresholdPrecedenceSurvivesThePluginMove);
    }

    private static void SharesOneAcquisitionAndRetainsTheOriginalObservationTime()
    {
        var clock = new TestTimeProvider();
        var source = Source();
        using var reader = new NvidiaTelemetryReader(source, clock);
        var first = reader.Read(null, 1000, CancellationToken.None)!;
        clock.Advance(TimeSpan.FromMilliseconds(700));
        var second = reader.Read(first.Device.LegacyId, 1000, CancellationToken.None)!;
        TestAssert.Equal(1, source.Reads);
        TestAssert.Equal(first.ObservedAt, second.ObservedAt);
        clock.Advance(TimeSpan.FromMilliseconds(301));
        reader.Read(first.Device.Id, 1000, CancellationToken.None);
        TestAssert.Equal(2, source.Reads);
    }

    private static void PreservesSelectedIdentityWhenEnumerationChanges()
    {
        var clock = new TestTimeProvider();
        var source = Source();
        using var reader = new NvidiaTelemetryReader(source, clock);
        var selected = reader.Read("pci-first", 1000, CancellationToken.None)!;
        source.Devices = [new("pci-other", "/gpu-nvidia/0", "NVIDIA GeForce RTX 4080"), new("pci-first", "/gpu-nvidia/1", "NVIDIA GeForce RTX 5090")];
        reader.GetDevices(refresh: true);
        TestAssert.Equal(selected.Device.Id, reader.Read("pci-first", 1000, CancellationToken.None)!.Device.Id);
        TestAssert.True(reader.Read("removed-device", 1000, CancellationToken.None) is null, "A missing explicit selection must not switch GPUs.");
    }

    private static void MapsSingleAndMultipleFansByCoolerIdWithoutInventingDuty()
    {
        var sample = Sample([
            new("GPU Fan", SensorType.Fan, 4, 1200),
            new("GPU Fan", SensorType.Control, 4, 40)]);
        var readings = GenericNvidiaSnapshotMapper.Create(sample);
        TestAssert.Equal(1, readings.Single(reading => reading.Identity.Metric == SensorMetric.FanSpeed).Identity.Channel);
        TestAssert.Equal<double?>(40, readings.Single(reading => reading.Identity.Metric == SensorMetric.FanDuty).Value);

        readings = GenericNvidiaSnapshotMapper.Create(Sample([
            new("GPU Fan 2", SensorType.Fan, 8, 1500),
            new("GPU Fan 1", SensorType.Fan, 4, 1200),
            new("GPU Fan 2", SensorType.Control, 8, 50)]));
        TestAssert.Equal<double?>(1200, readings.Single(reading => reading.Identity is { Metric: SensorMetric.FanSpeed, Channel: 1 }).Value);
        TestAssert.Equal(2, readings.Single(reading => reading.Identity.Metric == SensorMetric.FanDuty).Identity.Channel);

        readings = GenericNvidiaSnapshotMapper.Create(Sample([new("GPU", SensorType.Fan, 1, 0)]));
        TestAssert.Equal<double?>(0, readings.Single().Value);
    }

    private static void MapsCoreVoltageAndHotspotWithoutPublishingBoardPower()
    {
        var readings = GenericNvidiaSnapshotMapper.Create(Sample([
            new("GPU Core Voltage", SensorType.Voltage, 0, 0.925f),
            new("GPU Hot Spot", SensorType.Temperature, 2, 75),
            new("GPU Package", SensorType.Power, 0, 400)]));
        TestAssert.Equal(2, readings.Length);
        TestAssert.Equal(SensorUnit.Volts, readings.Single(reading => reading.Identity.Component == SensorComponent.GpuCore).Unit);
        TestAssert.Equal<double?>(75, readings.Single(reading => reading.Identity.Component == SensorComponent.GpuHotspot).Value);
    }

    private static void InputVoltageUsesMeasuredPinsAndNeverPowerDividedByCurrent()
    {
        var sensors = new[] { new NvidiaSensor("12VHPWR Connector", SensorType.Power, 0, 400), new NvidiaSensor("12VHPWR Connector", SensorType.Current, 0, 20) };
        TestAssert.False(GenericNvidiaSnapshotMapper.Create(Sample(sensors)).Any(reading => reading.Identity.Metric == SensorMetric.Voltage), "Missing measured input voltage must remain unavailable.");
        sensors = [.. sensors, .. Enumerable.Range(1, 6).Select(pin => new NvidiaSensor($"12VHPWR Pin {pin}", SensorType.Voltage, pin, 12))];
        TestAssert.Equal<double?>(12, GenericNvidiaSnapshotMapper.Create(Sample(sensors)).Single(reading => reading.Identity.Metric == SensorMetric.Voltage).Value);
    }

    private static void MigrationPreservesExistingThresholdsAndDefaults()
    {
        using var reader = new NvidiaTelemetryReader(Source(), new TestTimeProvider());
        var factory = new GenericNvidiaPluginFactory(reader);
        var fresh = GenericNvidiaSettings.GetOptions(factory.CreateDefaultSettings());
        TestAssert.Equal<double?>(81, fresh.CoreTemperatureWarningC);
        TestAssert.Equal<double?>(85, fresh.MemoryTemperatureWarningC);
        var legacy = new ServiceConfiguration { Plugins = new() { ["asus-astral"] = new() } };
        var upgraded = factory.MigrateConfiguration(legacy);
        var migrated = GenericNvidiaSettings.GetOptions(upgraded.Plugins[factory.Id]);
        TestAssert.Equal<double?>(84, migrated.CoreTemperatureWarningC);
        TestAssert.Equal<double?>(95, migrated.MemoryTemperatureWarningC);
        TestAssert.False(legacy.Plugins.ContainsKey(factory.Id), "Migration must not mutate its baseline.");
    }

    private static void LegacyThresholdPrecedenceSurvivesThePluginMove()
    {
        using var reader = new NvidiaTelemetryReader(Source(), new TestTimeProvider());
        var factory = new GenericNvidiaPluginFactory(reader);
        var legacy = new ServiceConfiguration
        {
            Plugins = new() { ["asus-astral"] = new() { Options = System.Text.Json.JsonSerializer.SerializeToElement(new { CoreTemperatureWarningC = 90 }) } },
            Warnings = new()
            {
                AdditionalSettings = new()
                {
                    ["coreTemperatureC"] = System.Text.Json.JsonSerializer.SerializeToElement(81),
                    ["memoryTemperatureC"] = System.Text.Json.JsonSerializer.SerializeToElement(87)
                }
            }
        };
        var migrated = factory.MigrateConfiguration(legacy);
        var options = GenericNvidiaSettings.GetOptions(migrated.Plugins[factory.Id]);
        TestAssert.Equal<double?>(90, options.CoreTemperatureWarningC);
        TestAssert.Equal<double?>(87, options.MemoryTemperatureWarningC);
        TestAssert.True(migrated.Warnings.AdditionalSettings is null, "Consumed legacy settings must not override the new plugin again.");
    }

    private static TestNvidiaHardwareSource Source() => new()
    {
        Devices = [new("pci-first", "/gpu-nvidia/0", "NVIDIA GeForce RTX 5090")],
        Sensors = [new("GPU Core", SensorType.Clock, 0, 1500)]
    };

    private static NvidiaSample Sample(NvidiaSensor[] sensors) => new(new("gpu", "/gpu-nvidia/0", "NVIDIA GeForce RTX 5090"), DateTimeOffset.UtcNow, sensors);
}
