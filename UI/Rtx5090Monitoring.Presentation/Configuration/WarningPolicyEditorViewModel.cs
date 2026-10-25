using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed class WarningPolicyEditorViewModel : ObservableObject
{
    private bool _loading;
    public event Action? Edited;
    public bool NotifyOnStart
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
    public bool RepeatWhileActive
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Changed();
            }
        }
    }
    public double RepeatIntervalSeconds
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Changed();
            }
        }
    } = 5;
    public double ClearAfterSeconds
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Changed();
            }
        }
    } = 2;

    public void Load(WarningPolicy policy)
    {
        _loading = true;
        try
        {
            NotifyOnStart = policy.NotifyOnStart;
            RepeatWhileActive = policy.RepeatWhileActive;
            RepeatIntervalSeconds = policy.RepeatIntervalSeconds;
            ClearAfterSeconds = policy.ClearAfterSeconds;
        }
        finally { _loading = false; }
    }
    public WarningPolicy Build()
    {
        var policy = new WarningPolicy
        {
            NotifyOnStart = NotifyOnStart, RepeatWhileActive = RepeatWhileActive,
            RepeatIntervalSeconds = NumericInput.Integer(RepeatIntervalSeconds, "Reminder interval"),
            ClearAfterSeconds = NumericInput.Integer(ClearAfterSeconds, "Recovery confirmation")
        };
        if (!policy.IsValid())
        {
            throw new ArgumentException("Reminders must be 5-3600 seconds and recovery confirmation 0-60 seconds.");
        }

        return policy;
    }
    private void Changed()
    {
        if (!_loading)
        {
            Edited?.Invoke();
        }
    }
}
