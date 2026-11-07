using System.Collections.ObjectModel;
using System.Text.Json;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Monitoring;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed class ConnectorProfilesEditorViewModel : EditableObject
{
    private bool _loading;
    private string? _appliedProfileId;
    public ObservableCollection<ConnectorProfileEditorViewModel> Profiles { get; } = [];
    public ConnectorProfileEditorViewModel? SelectedProfile
    {
        get;
        set
        {
            if (value is not null && Set(ref field, value) && !_loading)
            {
                NotifyEdited();
            }
        }
    }

    public void Load(ConnectorProfiles options, PluginCatalog? catalog)
    {
        _loading = true;
        try
        {
            _appliedProfileId = options.ActiveProfileId;
            Profiles.Clear();
            var ids = options.Profiles.SelectMany(profile => profile.Metrics).SelectMany(metric => metric.PreferredPlugins)
                .Concat(catalog?.Plugins.Where(plugin => plugin.ConnectorMetrics.Length > 0).Select(plugin => plugin.Id) ?? [])
                .Distinct(StringComparer.Ordinal).ToArray();
            var choices = ids.Select(id => new ConnectorPluginChoice(id,
                id == "generic-nvidia" ? "Nvidia" : id == "wireview-pro2" ? "WireView" : id == "asus-astral" ? "ROG Astral"
                : catalog?.Plugins.FirstOrDefault(plugin => plugin.Id == id)?.Name ?? id)).ToArray();

            foreach (var profile in options.Profiles)
            {
                var editor = new ConnectorProfileEditorViewModel(profile, choices, catalog);
                editor.Edited += NotifyEdited;
                Profiles.Add(editor);
            }

            SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == options.ActiveProfileId);
        }
        finally
        {
            _loading = false;
        }
    }

    public ConnectorProfiles Build() => new()
    {
        Version = Profiles.Count == 0 ? 0 : 1,
        ActiveProfileId = SelectedProfile?.Id ?? "automatic",
        Profiles = [.. Profiles.Select(profile => profile.Build())]
    };

    public bool Matches(ConnectorProfiles options) => JsonSerializer.Serialize(Build()) == JsonSerializer.Serialize(options);

    public void Apply(SensorSnapshot snapshot)
    {
        foreach (var profile in Profiles)
        {
            foreach (var metric in profile.Metrics)
            {
                var resolved = snapshot.Selections.FirstOrDefault(selection => selection.Name == ConnectorProfiles.SelectionName(metric.Metric));
                var reading = snapshot.Sensors.FirstOrDefault(reading => reading.Identity.Key == resolved?.SensorKey);
                metric.Resolution = profile != SelectedProfile || profile.Id != _appliedProfileId ? "Select this profile and apply to use it."
                    : reading is null ? "Unavailable - no usable connector source"
                    : $"{resolved!.State} - {new SensorChoice(reading.Identity).Label}";
            }
        }
    }
}
