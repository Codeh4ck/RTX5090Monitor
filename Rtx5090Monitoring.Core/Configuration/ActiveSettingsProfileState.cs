using Rtx5090Monitoring.Contracts.Configuration;

namespace Rtx5090Monitoring.Core.Configuration;

public sealed class ActiveSettingsProfileState
{
    private ActiveSettingsProfile? _current;

    public ActiveSettingsProfile? Current
    {
        get => Volatile.Read(ref _current);
        set => Volatile.Write(ref _current, value);
    }
}
