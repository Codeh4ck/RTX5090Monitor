using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed class WarningOverrideEditorViewModel : ObservableObject
{
    private bool _loading;
    public string TypeId { get; }
    public string Name { get; private set => Set(ref field, value); }
    public WarningPolicyEditorViewModel Policy { get; } = new();
    public event Action? Edited;
    public bool UseDefaults
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Raise(nameof(IsCustom));
                if (!_loading)
                {
                    Edited?.Invoke();
                }
            }
        }
    } = true;
    public bool IsCustom => !UseDefaults;
    public WarningOverrideEditorViewModel(WarningDefinition definition, string pluginName)
    {
        TypeId = definition.Id;
        Name = pluginName + " - " + definition.Title;
        Policy.Edited += () => { if (!_loading && IsCustom)
            {
                Edited?.Invoke();
            }
        };
    }
    internal void UpdateName(string pluginName, string title) => Name = pluginName + " - " + title;

    public void Load(bool inherited, WarningPolicy policy)
    {
        _loading = true;
        try { UseDefaults = inherited; Policy.Load(policy); }
        finally { _loading = false; }
    }
}
