using System.Reflection;
using Microsoft.UI.Dispatching;
using Rtx5090Monitoring.Client.Updates;
using Rtx5090Monitoring.Presentation.Updates;

namespace Rtx5090Monitoring.App.Services;

internal sealed partial class AppRuntime
{
    private readonly HttpClient _updateHttp = new() { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 16384 };
    private DispatcherQueueTimer? _updateTimer;

    public UpdateViewModel Updates { get; private set; } = null!;

    private void InitializeUpdates(string directory)
    {
        var version = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? "unknown";
        var client = new UpdateManifestClient(_updateHttp);
        Updates = new UpdateViewModel(version, client.GetAsync, async token => (await _api.GetVersionAsync(token))?.Version,
            new UpdateStateStore(Path.Combine(directory, _diagnostic || IsDemo ? "demo-updates.json" : "updates.json")));
        ConfigureUpdates();
        _updateTimer = _dispatcher.CreateTimer();
        _updateTimer.Interval = TimeSpan.FromMinutes(1);
        _updateTimer.Tick += (_, _) => _ = Updates.CheckAsync(false, StopToken);
    }

    private void ConfigureUpdates() => Updates.Configure(!IsDemo && Preferences.AutomaticallyCheckForUpdates, Preferences.UpdateFrequency);

    private void StartUpdates()
    {
        _updateTimer?.Start();
        _ = Updates.CheckAsync(false, StopToken);
    }
}
