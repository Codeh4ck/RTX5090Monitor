using System.Runtime.CompilerServices;

namespace Rtx5090Monitoring.Presentation.Infrastructure;

public abstract class EditableObject : ObservableObject
{
    public event Action? Edited;

    protected void Edit<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (Set(ref field, value, property))
        {
            NotifyEdited();
        }
    }

    protected void NotifyEdited() => Edited?.Invoke();
}
