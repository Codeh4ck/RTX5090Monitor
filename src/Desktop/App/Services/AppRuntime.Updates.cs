using System.Reflection;
using Microsoft.UI.Dispatching;
using RTXLens.Client.Updates;
using RTXLens.Presentation.Updates;

namespace RTXLens.App.Services;

internal sealed partial class AppRuntime
{
    private readonly HttpClient _updateHttp = new() { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 131072 };
    private DispatcherQueueTimer? _updateTimer;

    public UpdateViewModel Updates { get; private set; } = null!;

    private void InitializeUpdates(string directory)
    {
        var version = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? "unknown";
        var client = new UpdateManifestClient(_updateHttp);
        Updates = new UpdateViewModel(version, client.GetAsync, async token => (await _api.GetVersionAsync(token))?.Version,
            new UpdateStateStore(Path.Combine(directory, _diagnostic || IsDemo ? "demo-updates.json" : "updates.json")));
        Updates.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UpdateViewModel.HasUpdates))
            {
                PrefetchAvailableReleaseNotes();
            }
        };

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
