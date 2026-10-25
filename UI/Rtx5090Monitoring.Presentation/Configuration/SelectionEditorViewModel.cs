using System.Collections.ObjectModel;
using Rtx5090Monitoring.Contracts.Monitoring;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed class SelectionEditorViewModel : EditableObject
{
    private string _name;
    private bool _fallback;
    private bool _differentScope;

    public string Name { get => _name; set => Edit(ref _name, value); }
    public bool AllowFallback { get => _fallback; set => Edit(ref _fallback, value); }
    public bool AllowDifferentScope { get => _differentScope; set => Edit(ref _differentScope, value); }

    public SensorChoice? SelectedSource
    {
        get;
        set => Set(ref field, value);
    }

    public SensorChoice? Candidate
    {
        get;
        set => Set(ref field, value);
    }

    public string Resolution
    {
        get;
        set => Set(ref field, value);
    } = "Unavailable";

    public ObservableCollection<SensorChoice> Sources { get; }
    public ObservableCollection<SensorChoice> AvailableSensors { get; }

    public SelectionEditorViewModel(SensorSelection selection, ObservableCollection<SensorChoice> availableSensors)
    {
        _name = selection.Name;
        _fallback = selection.AllowFallback;
        _differentScope = selection.AllowDifferentScope;
        Sources = [.. selection.PreferredSources.Select(identity => new SensorChoice(identity))];
        Sources.CollectionChanged += (_, _) => NotifyEdited();
        AvailableSensors = availableSensors;
    }

    public void AddSource()
    {
        if (Candidate is not null && Sources.All(source => source.Identity.Key != Candidate.Identity.Key))
        {
            Sources.Add(Candidate);
            SelectedSource = Candidate;
        }
    }

    public void RemoveSource()
    {
        if (SelectedSource is not null)
        {
            Sources.Remove(SelectedSource);
        }
    }

    public void MoveSource(int direction)
    {
        if (SelectedSource is null)
        {
            return;
        }

        var index = Sources.IndexOf(SelectedSource);
        var destination = index + direction;
        if (index >= 0 && destination >= 0 && destination < Sources.Count)
        {
            Sources.Move(index, destination);
        }
    }

    public SensorSelection Build() => new()
    {
        Name = Name.Trim(),
        AllowFallback = AllowFallback,
        AllowDifferentScope = AllowDifferentScope,
        PreferredSources = [.. Sources.Select(source => source.Identity)]
    };

    public bool Matches(SensorSelection selection) => Name.Trim() == selection.Name
        && AllowFallback == selection.AllowFallback && AllowDifferentScope == selection.AllowDifferentScope
        && Sources.Select(source => source.Identity).SequenceEqual(selection.PreferredSources);
}