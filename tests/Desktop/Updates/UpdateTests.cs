using System.Net;
using System.Text.Json.Nodes;
using Rtx5090Monitoring.Client.Http;
using Rtx5090Monitoring.Contracts.Updates;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Presentation.Updates;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.UI.Tests.TestDoubles;

namespace Rtx5090Monitoring.UI.Tests.Updates;

internal static class UpdateTests
{
    public static async Task RunAsync(TestRunner runner)
    {
        runner.Run(ComparesSemanticVersionsNumerically);
        runner.Run(UsesCalendarMonthsAndRealWeeklyIntervals);
        await runner.RunAsync(ChecksOnStartupAndPersistsTheScheduledTimestampAsync);
        await runner.RunAsync(DisabledChecksStillAllowManualChecksAsync);
        await runner.RunAsync(DismissalsArePerComponentAndVersionAsync);
        await runner.RunAsync(RemindersExpireWithoutWaitingForTheWeeklyPollAsync);
        await runner.RunAsync(OfflineAndUnknownServiceVersionsDoNotBreakChecksAsync);
        await runner.RunAsync(OlderServicesCanOmitTheVersionEndpointAsync);
        await runner.RunAsync(MigratesUpdateDefaultsAndPersistsOptOutAsync);
    }

    private static void ComparesSemanticVersionsNumerically()
    {
        TestAssert.True(SemanticVersion.TryParse("1.10.0", out var latest), "Stable versions must parse.");
        TestAssert.True(SemanticVersion.TryParse("1.9.9", out var installed), "Stable versions must parse.");
        TestAssert.True(latest.CompareTo(installed) > 0, "Versions must not use lexical ordering.");

        foreach (var invalid in new[] { "01.0.0", "1.0", "1.0.0-preview", "-1.0.0", "1.0.0.0", "1.0.0+test" })
        {
            TestAssert.False(SemanticVersion.TryParse(invalid, out _), "Malformed and preview versions must not be advertised.");
        }

        TestAssert.False(new ReleaseManifest { ReleaseTag = null! }.IsValid(), "Malformed manifests must be rejected safely.");
    }

    private static void UsesCalendarMonthsAndRealWeeklyIntervals()
    {
        var start = new DateTimeOffset(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);
        TestAssert.Equal(start.AddDays(1), UpdateViewModel.NextCheck(start, UpdateFrequency.Daily));
        TestAssert.Equal(start.AddDays(7), UpdateViewModel.NextCheck(start, UpdateFrequency.Weekly));
        TestAssert.Equal(new DateTimeOffset(2026, 2, 28, 12, 0, 0, TimeSpan.Zero), UpdateViewModel.NextCheck(start, UpdateFrequency.Monthly));
    }

    private static async Task ChecksOnStartupAndPersistsTheScheduledTimestampAsync()
    {
        using var fixture = new UpdateTestFixture();
        var first = fixture.Create();
        first.Configure(true, UpdateFrequency.Weekly);
        await first.CheckAsync(false, default);
        await first.CheckAsync(false, default);
        TestAssert.Equal(1, fixture.Requests);
        fixture.Clock.Now = fixture.Clock.Now.AddDays(6);
        await first.CheckAsync(false, default);
        TestAssert.Equal(1, fixture.Requests);
        fixture.Clock.Now = fixture.Clock.Now.AddDays(1);
        await first.CheckAsync(false, default);
        TestAssert.Equal(2, fixture.Requests);

        var restarted = fixture.Create();
        TestAssert.False(restarted.LastChecked == "Never", "The last successful check must survive restart.");
        restarted.Configure(true, UpdateFrequency.Weekly);
        await restarted.CheckAsync(false, default);
        TestAssert.Equal(3, fixture.Requests);
    }

    private static async Task DisabledChecksStillAllowManualChecksAsync()
    {
        using var fixture = new UpdateTestFixture();
        var model = fixture.Create();
        model.Configure(false, UpdateFrequency.Daily);
        await model.CheckAsync(false, default);
        TestAssert.Equal(0, fixture.Requests);
        await model.CheckAsync(true, default);
        TestAssert.Equal(1, fixture.Requests);
        TestAssert.Equal(2, model.Available.Count);
    }

    private static async Task DismissalsArePerComponentAndVersionAsync()
    {
        using var fixture = new UpdateTestFixture();
        var model = fixture.Create();
        await model.CheckAsync(false, default);
        await model.DismissAsync(model.Available.Single(notice => notice.Component == "desktop"), true, default);
        TestAssert.Equal("service", model.Available.Single().Component);

        var restarted = fixture.Create();
        await restarted.CheckAsync(false, default);
        TestAssert.Equal("service", restarted.Available.Single().Component);
        fixture.Manifest = fixture.Manifest with { Desktop = "1.2.0", ReleaseTag = "v1.2.0" };
        await restarted.CheckAsync(true, default);
        TestAssert.Equal(2, restarted.Available.Count);
        TestAssert.True(restarted.Available.All(notice => notice.ReleaseUrl.EndsWith("/v1.2.0", StringComparison.Ordinal)),
            "Every notice must link to the release that supplied its manifest.");
    }

    private static async Task RemindersExpireWithoutWaitingForTheWeeklyPollAsync()
    {
        using var fixture = new UpdateTestFixture();
        var model = fixture.Create();
        model.Configure(true, UpdateFrequency.Weekly);
        await model.CheckAsync(false, default);

        foreach (var notice in model.Available.ToArray())
        {
            await model.DismissAsync(notice, false, default);
        }

        TestAssert.False(model.HasUpdates, "Remind later hides the current notices.");
        fixture.Clock.Now = fixture.Clock.Now.AddHours(24);
        await model.CheckAsync(false, default);
        TestAssert.Equal(1, fixture.Requests);
        TestAssert.Equal(2, model.Available.Count);
    }

    private static async Task OfflineAndUnknownServiceVersionsDoNotBreakChecksAsync()
    {
        using var fixture = new UpdateTestFixture { Fail = true };
        var model = fixture.Create();
        await model.CheckAsync(false, default);
        await model.CheckAsync(false, default);
        TestAssert.Equal(1, fixture.Requests);
        TestAssert.False(model.Checking, "Offline checks must complete without throwing.");
        fixture.Fail = false;
        fixture.ServiceVersion = null;
        await model.CheckAsync(true, default);
        TestAssert.Equal("desktop", model.Available.Single().Component);
        TestAssert.Equal("Version unavailable", model.ServiceVersion);
    }

    private static async Task OlderServicesCanOmitTheVersionEndpointAsync()
    {
        using var http = new HttpClient(new VersionResponseHandler(HttpStatusCode.NotFound)) { BaseAddress = new Uri("http://localhost:59273/") };
        TestAssert.True(await new MonitoringApiClient(http).GetVersionAsync(default) is null, "An older service must remain usable.");
    }

    private static async Task MigratesUpdateDefaultsAndPersistsOptOutAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "rtx-update-preferences-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var store = new PreferencesStore(path);
            await store.SaveAsync(new AppPreferences());
            var legacy = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            legacy.Remove("automaticallyCheckForUpdates");
            legacy.Remove("updateFrequency");
            await File.WriteAllTextAsync(path, legacy.ToJsonString());
            var preferences = store.Load();
            TestAssert.True(preferences.AutomaticallyCheckForUpdates, "Existing users should receive the enabled default.");
            TestAssert.Equal(UpdateFrequency.Daily, preferences.UpdateFrequency);
            await store.SaveAsync(preferences with { AutomaticallyCheckForUpdates = false, UpdateFrequency = UpdateFrequency.Monthly });
            TestAssert.False(store.Load().AutomaticallyCheckForUpdates, "An explicit opt-out must be retained.");
            TestAssert.Equal(UpdateFrequency.Monthly, store.Load().UpdateFrequency);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
