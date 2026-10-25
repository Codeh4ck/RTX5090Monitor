using System.Collections.ObjectModel;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed class WarningOptionsEditorViewModel : ObservableObject
{
    private bool _loading;
    private WarningOptions _loaded = new();
    private readonly Dictionary<string, WarningOverrideEditorViewModel> _editors = new(StringComparer.Ordinal);
    public event Action? Edited;
    public WarningPolicyEditorViewModel Defaults { get; } = new();
    public ObservableCollection<WarningOverrideEditorViewModel> Overrides { get; } = [];
    public bool NotificationsEnabled
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Changed();
            }
        }
    } = true;
    public bool HasDefinitions => Overrides.Count > 0;

    public WarningOptionsEditorViewModel()
    {
        Defaults.Edited += () => { RefreshInherited(); Changed(); };
    }
    public void SetCatalogue(PluginCatalog catalog)
    {
        var definitions = catalog.Plugins.SelectMany(plugin => (plugin.Warnings ?? []).Select(definition => (Definition: definition, PluginName: plugin.Name))).ToArray();
        if (Overrides.Select(item => (item.TypeId, item.Name)).SequenceEqual(definitions.Select(item => (item.Definition.Id, item.PluginName + " - " + item.Definition.Title))))
        {
            return;
        }

        var desired = new List<WarningOverrideEditorViewModel>();
        foreach (var item in definitions)
        {
            if (!_editors.TryGetValue(item.Definition.Id, out var editor))
            {
                editor = new WarningOverrideEditorViewModel(item.Definition, item.PluginName);
                editor.Load(!_loaded.Overrides.ContainsKey(editor.TypeId), _loaded.PolicyFor(editor.TypeId));
                editor.Edited += () => { RefreshInherited(); Changed(); };
                _editors.Add(editor.TypeId, editor);
            }
            editor.UpdateName(item.PluginName, item.Definition.Title);
            desired.Add(editor);
        }
        foreach (var removed in Overrides.Except(desired).ToArray())
        {
            Overrides.Remove(removed);
        }

        for (var index = 0; index < desired.Count; index++)
        {
            var current = Overrides.IndexOf(desired[index]);
            if (current < 0)
            {
                Overrides.Insert(index, desired[index]);
            }
            else if (current != index)
            {
                Overrides.Move(current, index);
            }
        }
        RefreshInherited();
        Raise(nameof(HasDefinitions));
    }
    private void RefreshInherited()
    {
        try
        {
            var policy = Defaults.Build();
            foreach (var item in _editors.Values.Where(item => item.UseDefaults))
            {
                item.Load(true, policy);
            }
        }
        catch (ArgumentException) { }
    }
    public void Load(WarningOptions options)
    {
        _loading = true;
        try
        {
            _loaded = options;
            NotificationsEnabled = options.NotificationsEnabled;
            Defaults.Load(options.Defaults);
            foreach (var item in _editors.Values)
            {
                item.Load(!options.Overrides.ContainsKey(item.TypeId), options.PolicyFor(item.TypeId));
            }
        }
        finally { _loading = false; }
    }
    public WarningOptions Build()
    {
        // Preserve policies for temporarily absent plugins instead of silently deleting them.
        var overrides = _loaded.Overrides.Where(pair => !_editors.ContainsKey(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var item in _editors.Values.Where(item => item.IsCustom))
        {
            overrides[item.TypeId] = item.Policy.Build();
        }

        var options = _loaded with { NotificationsEnabled = NotificationsEnabled, Defaults = Defaults.Build(), Overrides = overrides };
        if (!options.IsValid())
        {
            throw new ArgumentException("Invalid warning notification policy.");
        }

        return options;
    }
    public bool Matches(WarningOptions options)
    {
        try
        {
            var candidate = Build();
            return candidate.NotificationsEnabled == options.NotificationsEnabled && candidate.Defaults == options.Defaults
                && candidate.Overrides.Count == options.Overrides.Count
                && candidate.Overrides.All(pair => options.Overrides.TryGetValue(pair.Key, out var policy) && policy == pair.Value);
        }
        catch (ArgumentException) { return false; }
    }
    private void Changed()
    {
        if (!_loading)
        {
            Edited?.Invoke();
        }
    }
}
