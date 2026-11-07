using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Presentation.Performance;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.UI.Tests.Performance;

internal static class PerformanceRecordingTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(RecordsMultipleSourcesWithoutFallbackOrDuplicateSamples);
        runner.Run(SelectionChangesRetainPreviousSamplesAndCreateGaps);
        runner.Run(DisconnectsStaleReadingsAndModeChangesBreakTheTrace);
        runner.Run(StopsAtTheMemoryLimitAndExportsAllCapturedTraces);
        runner.Run(DiscoversOnlySupportedMetricUnitsAndUniqueColors);
        runner.Run(ExportsOfflineHtmlWithSafeEmbeddedData);
        runner.Run(ImportsRecordingFilesAndEarlierHtmlExports);
        runner.Run(RejectsInvalidImportsWithoutChangingTheCurrentSession);
        runner.Run(ImportedSessionsPreserveGapsAndLiveSourceAvailability);
        runner.Run(DisabledPluginsDisappearAndReturnWithoutLosingRecordedSamples);
    }

    private static void RecordsMultipleSourcesWithoutFallbackOrDuplicateSamples()
    {
        var clock = new RecordingClock();
        var recorder = Create(clock);
        var start = Frame(clock, 1);
        recorder.Accept(start);
        var keys = recorder.ReadFrame(null, new Dictionary<string, int>()).Series
            .Where(series => series.Trace.Group == "Connector power").Select(series => series.Trace.Key).ToArray();
        TestAssert.Equal(2, keys.Length);
        foreach (var key in keys)
        {
            recorder.Select(key, true);
        }

        recorder.Start();
        recorder.Accept(start);
        recorder.Accept(start);
        clock.Advance(1);
        recorder.Accept(Frame(clock, 2));
        recorder.Stop();

        var session = recorder.CompletedSession();
        TestAssert.Equal(2, session.Series.Length);
        TestAssert.Equal(4L, session.SampleCount);
        TestAssert.True(session.Series.All(series => series.Samples.Length == 2), "The two power sources must stay independent and deduplicate timestamps.");
        TestAssert.True(session.Series[0].Trace.Color != session.Series[1].Trace.Color, "Each source needs its own color.");

        var cursors = new Dictionary<string, int>();
        recorder.ReadFrame(null, cursors);
        var next = recorder.ReadFrame(session.SessionId, cursors);
        TestAssert.True(next.Series.All(series => series.Samples.Length == 0), "Incremental frames must not resend captured points.");
    }

    private static void SelectionChangesRetainPreviousSamplesAndCreateGaps()
    {
        var clock = new RecordingClock();
        var recorder = Create(clock);
        recorder.Accept(Frame(clock, 1));
        var options = recorder.ReadFrame(null, new Dictionary<string, int>()).Series;
        var power = options.First(series => series.Trace.Group == "Connector power").Trace.Key;
        var voltage = options.Single(series => series.Trace.Group == "GPU core voltage").Trace.Key;
        recorder.Select(power, true);
        recorder.Start();
        recorder.Accept(Frame(clock, 1));

        clock.Advance(1);
        recorder.Select(power, false);
        recorder.Select(voltage, true);
        recorder.Accept(Frame(clock, 2));
        clock.Advance(1);
        recorder.Select(power, true);
        recorder.Accept(Frame(clock, 3));
        recorder.Stop();

        var session = recorder.CompletedSession();
        var powerSamples = session.Series.Single(series => series.Trace.Key == power).Samples;
        TestAssert.Equal(3, powerSamples.Length);
        TestAssert.Equal<double?>(null, powerSamples[1].Value);
        var voltageSamples = session.Series.Single(series => series.Trace.Key == voltage).Samples;
        TestAssert.Equal(1d, voltageSamples[0].Seconds);
        TestAssert.Equal<double?>(850, voltageSamples[0].Value);

        recorder.Select(power, false);
        TestAssert.Equal(3, recorder.CompletedSession().Series.Single(series => series.Trace.Key == power).Samples.Length);
    }

    private static void DisconnectsStaleReadingsAndModeChangesBreakTheTrace()
    {
        var clock = new RecordingClock();
        var recorder = Create(clock);
        recorder.Accept(Frame(clock, 1));
        var key = recorder.ReadFrame(null, new Dictionary<string, int>()).Series
            .First(series => series.Trace.Group == "Connector power").Trace.Key;
        recorder.Select(key, true);
        recorder.Start();
        recorder.Accept(Frame(clock, 1));
        recorder.MarkDisconnected();
        clock.Advance(1);
        recorder.Accept(Frame(clock, 2));
        clock.Advance(6);
        recorder.Accept(Frame(clock, 3, oldBySeconds: 6));
        var unavailable = recorder.ReadFrame(null, new Dictionary<string, int>());
        TestAssert.False(unavailable.Series.Single(series => series.Trace.Key == key).Available, "Expired readings must be unavailable.");
        TestAssert.Equal(2, unavailable.Series.Single(series => series.Trace.Key == key).Samples.Count(sample => sample.Value is null));

        recorder.SwitchMode("simulation");
        var stopped = recorder.CompletedSession();
        TestAssert.False(stopped.Recording, "A mode change must finish the session.");
        TestAssert.True(stopped.Status.Contains("mode changed"), "The stop reason must explain a mode change.");
        recorder.Accept(Frame(clock, 4));
        TestAssert.Equal(2L, recorder.CompletedSession().SampleCount);
    }

    private static void StopsAtTheMemoryLimitAndExportsAllCapturedTraces()
    {
        var clock = new RecordingClock();
        var recorder = Create(clock, maximumSamples: 2);
        recorder.Accept(Frame(clock, 1));
        var key = recorder.ReadFrame(null, new Dictionary<string, int>()).Series.First().Trace.Key;
        recorder.Select(key, true);
        recorder.Start();
        recorder.Accept(Frame(clock, 1));
        clock.Advance(1);
        recorder.Accept(Frame(clock, 2));
        clock.Advance(1);
        recorder.Accept(Frame(clock, 3));
        var session = recorder.CompletedSession();
        TestAssert.Equal(2L, session.SampleCount);
        TestAssert.False(session.Recording, "A full session must finish without discarding samples.");
        TestAssert.True(session.Status.Contains("storage limit"), "The limit must be visible to the user.");

        recorder.Select(key, false);
        TestAssert.Equal(1, recorder.CompletedSession().Series.Length);
        recorder.Select(key, true);
        recorder.Start();
        TestAssert.Equal(0L, recorder.ReadFrame(null, new Dictionary<string, int>()).SampleCount);
    }

    private static void DiscoversOnlySupportedMetricUnitsAndUniqueColors()
    {
        var clock = new RecordingClock();
        var recorder = Create(clock);
        var frame = Frame(clock, 1);
        var current = new SensorIdentity("msi-psu", "psu0", SensorComponent.PsuPin, SensorMetric.Current, 1, "2");
        recorder.Accept(frame with
        {
            Plugins = [.. frame.Plugins, new PluginHealth("msi-psu", PluginState.Ready, clock.GetUtcNow(), null)],
            Sensors =
            [
                .. frame.Sensors,
                new(current, SensorUnit.Amperes, 2, clock.GetUtcNow()),
                new(current with { Metric = SensorMetric.FanSpeed }, SensorUnit.Rpm, 500, clock.GetUtcNow()),
                new(current with { Channel = 2 }, SensorUnit.Volts, 12, clock.GetUtcNow())
            ]
        });

        var series = recorder.ReadFrame(null, new Dictionary<string, int>()).Series;
        TestAssert.Equal(1, series.Count(item => item.Trace.Identity.PluginId == "msi-psu"));
        TestAssert.True(series.Single(item => item.Trace.Identity.PluginId == "msi-psu").Trace.Label.Contains("Connector 2"), "PSU connectors must be labeled.");
        TestAssert.Equal(series.Length, series.Select(item => item.Trace.Color).Distinct().Count());
    }

    private static void ExportsOfflineHtmlWithSafeEmbeddedData()
    {
        var session = Fixture();
        var malicious = session.Series[0].Trace with { Label = "</script><script>alert(1)</script>" };
        session = session with { Series = [session.Series[0] with { Trace = malicious }, .. session.Series.Skip(1)] };
        var html = PerformanceHtmlExporter.Export(session);

        TestAssert.False(html.Contains(malicious.Label), "Sensor labels must never terminate the JSON script element.");
        TestAssert.True(html.Contains("\\u003C/script"), "HTML-sensitive characters should be JSON escaped.");
        TestAssert.False(html.Contains("src=\"http"), "The HTML must work offline.");
        TestAssert.True(html.Contains("window-label"), "The exported chart must include its time navigation controls.");
        TestAssert.Throws<ArgumentException>(() => PerformanceHtmlExporter.Export(session with { Recording = true }));
    }

    private static void ImportsRecordingFilesAndEarlierHtmlExports()
    {
        var original = Fixture();
        var dataFile = PerformanceRecordingFile.Serialize(original);
        var imported = PerformanceRecordingFile.Deserialize(dataFile);
        AssertSameSamples(original, imported);

        var html = PerformanceHtmlExporter.Export(original);
        AssertSameSamples(original, PerformanceRecordingFile.Deserialize(html));

        var legacyJson = System.Text.Json.JsonSerializer.Serialize(original,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var earlierHtml = "<html><script id='session-data' type='application/json'>" + legacyJson + "</script></html>";
        AssertSameSamples(original, PerformanceRecordingFile.Deserialize(earlierHtml));
        TestAssert.True(dataFile.Contains("\"schemaVersion\":1"), "The recording data file needs an explicit format version.");
    }

    private static void RejectsInvalidImportsWithoutChangingTheCurrentSession()
    {
        var original = Fixture();
        var clock = new RecordingClock();
        var recorder = Create(clock);
        recorder.Import(original);
        TestAssert.Throws<InvalidDataException>(() => PerformanceRecordingFile.Deserialize("{}"));
        TestAssert.Throws<InvalidDataException>(() => PerformanceRecordingFile.Deserialize("<html>No recording</html>"));
        var unsupported = PerformanceRecordingFile.Serialize(original).Replace("\"schemaVersion\":1", "\"schemaVersion\":2");
        TestAssert.Throws<InvalidDataException>(() => PerformanceRecordingFile.Deserialize(unsupported));

        var duplicate = original with { Series = [original.Series[0], original.Series[0]] };
        TestAssert.Throws<InvalidDataException>(() => recorder.Import(duplicate));
        var series = original.Series[0];
        var backwards = original with { Series = [series with { Samples = [new(2, 1), new(1, 2)], Count = 2 }], SampleCount = 2 };
        TestAssert.Throws<InvalidDataException>(() => recorder.Import(backwards));

        AssertSameSamples(original, recorder.CompletedSession());
    }

    private static void ImportedSessionsPreserveGapsAndLiveSourceAvailability()
    {
        var clock = new RecordingClock();
        var recorder = Create(clock);
        recorder.Accept(Frame(clock, 1));
        var imported = Fixture();
        recorder.Import(imported);
        AssertSameSamples(imported, recorder.CompletedSession());
        var options = recorder.ReadFrame(null, new Dictionary<string, int>());
        TestAssert.True(options.Series.All(series => series.Available), "Local reporting sources should remain available after importing their history.");
        recorder.Start();
        TestAssert.Throws<InvalidOperationException>(() => recorder.Import(imported));
        recorder.Accept(Frame(clock, 1));
        recorder.Stop();
        TestAssert.Equal(10L, recorder.CompletedSession().SampleCount);
    }

    private static void DisabledPluginsDisappearAndReturnWithoutLosingRecordedSamples()
    {
        var clock = new RecordingClock();
        var recorder = Create(clock);
        recorder.Accept(Frame(clock, 1));
        var powers = recorder.ReadFrame(null, new Dictionary<string, int>()).Series
            .Where(series => series.Trace.Group == "Connector power").ToArray();
        foreach (var series in powers)
        {
            recorder.Select(series.Trace.Key, true);
        }

        recorder.Start();
        recorder.Accept(Frame(clock, 1));
        var cursors = new Dictionary<string, int>();
        var initial = recorder.ReadFrame(null, cursors);
        var astralKey = powers.Single(series => series.Trace.Identity.PluginId == "asus-astral").Trace.Key;
        var configuration = new ServiceConfiguration
        {
            Plugins = new Dictionary<string, PluginSettings>
            {
                ["asus-astral"] = new() { Enabled = false },
                ["wireview-pro2"] = new(),
                ["generic-nvidia"] = new(),
                ["msi-psu"] = new()
            }
        };

        recorder.SetConfiguration(configuration);
        var hidden = recorder.ReadFrame(initial.SessionId, cursors);
        TestAssert.False(hidden.Series.Any(series => series.Trace.Identity.PluginId == "asus-astral"),
            "Disabling a plugin must immediately remove its sources from the live chart.");
        TestAssert.False(cursors.ContainsKey(astralKey), "Restoring a hidden trace must send its complete history.");
        clock.Advance(1);
        recorder.Accept(Frame(clock, 2));
        recorder.Stop();
        var completed = recorder.CompletedSession();
        TestAssert.Equal(3L, completed.SampleCount);
        TestAssert.True(completed.Series.Any(series => series.Trace.Key == astralKey),
            "Captured readings must remain in the saved session after disabling their plugin.");

        configuration.Plugins["asus-astral"] = new();
        recorder.SetConfiguration(configuration);
        var restored = recorder.ReadFrame(initial.SessionId, cursors).Series.Single(series => series.Trace.Key == astralKey);
        TestAssert.Equal(0, restored.Offset);
        TestAssert.True(restored.Samples.Any(sample => sample.Value is not null), "Re-enabling a plugin must restore its captured history.");

        configuration.Plugins["asus-astral"] = new() { Enabled = false };
        recorder.SetConfiguration(configuration);
        recorder.Import(completed);
        TestAssert.True(recorder.ReadFrame(null, new Dictionary<string, int>()).Series.Any(series => series.Trace.Key == astralKey),
            "An imported recording must remain reviewable independently of the machine's live plugin settings.");

        configuration.Plugins["generic-nvidia"] = new() { Enabled = false };
        recorder.SetConfiguration(configuration);
        TestAssert.False(recorder.ReadFrame(null, new Dictionary<string, int>()).Series.Any(series => series.Trace.Identity.PluginId == "generic-nvidia"),
            "Disabling a live source outside the imported recording must still hide that source.");
    }

    private static void AssertSameSamples(PerformanceChartFrame expected, PerformanceChartFrame actual)
    {
        TestAssert.Equal(expected.StartedAt, actual.StartedAt);
        TestAssert.Equal(expected.DurationSeconds, actual.DurationSeconds);
        TestAssert.Equal(expected.SampleCount, actual.SampleCount);
        TestAssert.Equal(expected.Series.Length, actual.Series.Length);
        for (var index = 0; index < expected.Series.Length; index++)
        {
            TestAssert.Equal(expected.Series[index].Trace, actual.Series[index].Trace);
            TestAssert.Equal(expected.Series[index].Selected, actual.Series[index].Selected);
            TestAssert.True(expected.Series[index].Samples.SequenceEqual(actual.Series[index].Samples), "Import must retain every recorded value and gap.");
        }
    }

    internal static PerformanceChartFrame Fixture()
    {
        var clock = new RecordingClock();
        var recorder = Create(clock);
        recorder.Accept(Frame(clock, 1));
        foreach (var option in recorder.ReadFrame(null, new Dictionary<string, int>()).Series)
        {
            recorder.Select(option.Trace.Key, true);
        }

        recorder.Start();
        for (var index = 0; index <= 120; index++)
        {
            if (index == 50)
            {
                recorder.MarkDisconnected();
            }

            recorder.Accept(Frame(clock, index));
            clock.Advance(1);
        }

        recorder.Stop();
        return recorder.CompletedSession();
    }

    private static PerformanceRecorder Create(RecordingClock clock, int maximumSamples = 2_000_000)
    {
        var recorder = new PerformanceRecorder(clock, maximumSamples);
        recorder.SetConfiguration(new ServiceConfiguration
        {
            Plugins = new Dictionary<string, PluginSettings>
            {
                ["asus-astral"] = new(),
                ["wireview-pro2"] = new(),
                ["generic-nvidia"] = new(),
                ["msi-psu"] = new()
            }
        });
        return recorder;
    }

    private static SensorSnapshot Frame(RecordingClock clock, int sequence, int oldBySeconds = 0)
    {
        var now = clock.GetUtcNow();
        var observedAt = now.AddSeconds(-oldBySeconds);
        var sensors = new List<SensorReading>
        {
            new(new("asus-astral", "gpu0", SensorComponent.GpuConnector, SensorMetric.Power, 1), SensorUnit.Watts, 100 + sequence, observedAt),
            new(new("wireview-pro2", "com3", SensorComponent.GpuConnector, SensorMetric.Power, 1), SensorUnit.Watts, 110 + sequence, observedAt),
            new(new("generic-nvidia", "gpu0", SensorComponent.GpuCore, SensorMetric.Voltage), SensorUnit.Volts, 0.85, observedAt),
            new(new("generic-nvidia", "gpu0", SensorComponent.GpuCore, SensorMetric.Temperature), SensorUnit.Celsius, 40 + sequence / 10d, observedAt)
        };
        for (var pin = 1; pin <= 6; pin++)
        {
            sensors.Add(new(new("asus-astral", "gpu0", SensorComponent.GpuPin, SensorMetric.Current, pin), SensorUnit.Amperes, 1 + pin / 10d + sequence / 100d, observedAt));
        }

        return new(1, sequence, now, [.. sensors],
            [.. new[] { "asus-astral", "wireview-pro2", "generic-nvidia" }
                .Select(plugin => new PluginHealth(plugin, PluginState.Ready, observedAt, null))], []);
    }

    private sealed class RecordingClock : TimeProvider
    {
        private readonly DateTimeOffset _start = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        private long _ticks;

        public override DateTimeOffset GetUtcNow() => _start.AddTicks(_ticks);
        public override long GetTimestamp() => _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(int seconds) => _ticks += TimeSpan.FromSeconds(seconds).Ticks;
    }
}
