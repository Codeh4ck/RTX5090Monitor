using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Rtx5090Monitoring.Client.Serialization;
using Rtx5090Monitoring.Contracts.Sensors;

namespace Rtx5090Monitoring.Client.Streaming;

public static class SensorFrameReader
{
    public const int MaximumFrameBytes = 1024 * 1024;

    public static async IAsyncEnumerable<SensorSnapshot> ReadAsync(WebSocket socket, [EnumeratorCancellation] CancellationToken token)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var message = new MemoryStream(16384);
            while (!token.IsCancellationRequested)
            {
                message.SetLength(0);
                ValueWebSocketReceiveResult received;
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(100));
                do
                {
                    received = await socket.ReceiveAsync(buffer.AsMemory(), deadline.Token);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged", token);
                        yield break;
                    }

                    if (received.MessageType != WebSocketMessageType.Text || message.Length + received.Count > MaximumFrameBytes)
                    {
                        throw new InvalidDataException("Unsupported or oversized sensor frame.");
                    }

                    message.Write(buffer, 0, received.Count);
                } while (!received.EndOfMessage);

                var frame = JsonSerializer.Deserialize(message.GetBuffer().AsSpan(0, (int)message.Length), ClientJsonContext.Default.SensorSnapshot)
                    ?? throw new InvalidDataException("Empty sensor frame.");
                if (frame.SchemaVersion != 1 || frame.Sensors is null || frame.Plugins is null || frame.Selections is null || frame.Alerts is null)
                {
                    throw new InvalidDataException("Unsupported sensor schema. Update the desktop app and service together.");
                }

                if (frame.Warnings is { } warnings && !ValidWarnings(warnings))
                {
                    throw new InvalidDataException("Invalid shared warning state.");
                }
                yield return frame;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    private static bool ValidWarnings(Contracts.Warnings.WarningSnapshot warnings)
    {
        if (string.IsNullOrWhiteSpace(warnings.InstanceId) || warnings.Sequence < 0 || warnings.States is null || warnings.States.Length > 256)
        {
            return false;
        }
        if (warnings.States.Any(state => state is null || string.IsNullOrWhiteSpace(state.Id) || string.IsNullOrWhiteSpace(state.EpisodeId)
            || string.IsNullOrWhiteSpace(state.PluginId) || string.IsNullOrWhiteSpace(state.DeviceId)
            || string.IsNullOrWhiteSpace(state.TypeId) || !Enum.IsDefined(state.Phase) || state.Policy is null || !state.Policy.IsValid()
            || state.NotificationSequence < 0 || state.NotificationSequence > warnings.Sequence))
        {
            return false;
        }
        return warnings.States.Select(state => state.Id).Distinct(StringComparer.Ordinal).Count() == warnings.States.Length;
    }
}
