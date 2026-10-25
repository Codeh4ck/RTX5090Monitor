using System.Text.Json;

namespace Rtx5090Monitoring.Presentation.Updates;

public sealed class UpdateStateStore(string path)
{
    private readonly SemaphoreSlim _gate = new(1);

    public UpdateCheckState Load()
    {
        try
        {
            var state = File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), UpdateJsonContext.Default.UpdateCheckState)
                : null;
            state ??= new UpdateCheckState();
            state.Dismissals ??= [];

            if (state.Manifest is not null && !state.Manifest.IsValid())
            {
                state.Manifest = null;
            }

            return state;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new UpdateCheckState();
        }
    }

    public async Task SaveAsync(UpdateCheckState state, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state, UpdateJsonContext.Default.UpdateCheckState), token);
            File.Move(temporary, path, true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
