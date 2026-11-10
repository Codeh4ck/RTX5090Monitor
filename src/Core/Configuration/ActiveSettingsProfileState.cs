using RTXLens.Contracts.Configuration;

namespace RTXLens.Core.Configuration;

public sealed class ActiveSettingsProfileState
{
    private ActiveSettingsProfile? _current;

    public ActiveSettingsProfile? Current
    {
        get => Volatile.Read(ref _current);
        set => Volatile.Write(ref _current, value);
    }
}
