using System.Text.Json.Nodes;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.UI.Tests.Settings;

internal static class PreferenceTests
{
    public static async Task RunAsync(TestRunner runner)
    {
        runner.Run(RejectsInvalidPreferences);
        await runner.RunAsync(PersistsAndRecoversFromCorruptPreferencesAsync);
        await runner.RunAsync(MigratesWindowHeightOnceWithoutLosingPreferencesAsync);
    }

    private static async Task MigratesWindowHeightOnceWithoutLosingPreferencesAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "rtx-window-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            TestAssert.Equal(1050d, new AppPreferences().WindowHeight);
            var store = new PreferencesStore(path);
            await store.SaveAsync(new AppPreferences { WindowWidth = 1120, WindowHeight = 800, Theme = AppTheme.Dark, StartMinimized = true });
            var legacy = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            legacy.Remove("windowLayoutVersion");
            await File.WriteAllTextAsync(path, legacy.ToJsonString());
            var migrated = store.Load();
            TestAssert.True(store.LastError is null, store.LastError ?? "Legacy preferences must load successfully.");
            TestAssert.Equal((double)AppPreferences.DefaultWindowHeight, migrated.WindowHeight);
            TestAssert.Equal(1120d, migrated.WindowWidth);
            TestAssert.True(migrated is { Theme: AppTheme.Dark, StartMinimized: true }, "Migrating window height must preserve other preferences.");
            await store.SaveAsync(migrated with { WindowHeight = 900 });
            TestAssert.Equal(900d, store.Load().WindowHeight);
            legacy["windowHeight"] = 1200;
            await File.WriteAllTextAsync(path, legacy.ToJsonString());
            TestAssert.Equal(1200d, store.Load().WindowHeight);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void RejectsInvalidPreferences()
    {
        TestAssert.Throws<ArgumentException>(() => (new AppPreferences { AccentColor = null! }).Validate());
        TestAssert.Throws<ArgumentException>(() => (new AppPreferences { ServiceAddress = "file:///C:/Windows/" }).Validate());
        TestAssert.Throws<ArgumentException>(() => (new AppPreferences { ServiceAddress = "http://user:secret@localhost/" }).Validate());
        TestAssert.Throws<ArgumentException>(() => (new AppPreferences { TextScale = double.NaN }).Validate());
        TestAssert.Throws<ArgumentException>(() => (new AppPreferences { WireViewPsuConnector = 0 }).Validate());
        TestAssert.Throws<ArgumentException>(() => (new AppPreferences { AsusPsuConnector = 3 }).Validate());
    }

    private static async Task PersistsAndRecoversFromCorruptPreferencesAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "rtx-ui-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new PreferencesStore(path);
            var value = new AppPreferences { Theme = AppTheme.Light, StartMinimized = true, ShowWireViewPsuCurrent = true,
                WireViewPsuConnector = 2, ShowAsusPsuCurrent = true, AsusPsuConnector = 1 };
            await store.SaveAsync(value);
            TestAssert.Equal(value, store.Load());
            await File.WriteAllTextAsync(path, "{broken");
            TestAssert.Equal(new AppPreferences(), store.Load());
            TestAssert.True(store.LastError is not null, "Corrupt preferences must be reported.");
        }
        finally { File.Delete(path); }
    }
}
