using System.Net.WebSockets;
using System.Text.Json;

namespace Rtx5090Monitoring.Tests.Infrastructure;

internal static class WebSocketMessageReader
{
    public static async Task<JsonElement> ReadUntilAsync(ClientWebSocket socket, Func<JsonElement, bool> predicate, CancellationToken token)
    {
        while (true)
        {
            using var document = await ReadJsonAsync(socket, token);
            if (predicate(document.RootElement))
            {
                return document.RootElement.Clone();
            }
        }
    }

    public static async Task<JsonDocument> ReadJsonAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        using var message = new MemoryStream();
        var buffer = new byte[1024];
        ValueWebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new InvalidOperationException("Expected a JSON text frame.");
            }

            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return JsonDocument.Parse(message.ToArray());
    }

    public static async Task WaitForCloseAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        ValueWebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
        }
        while (result.MessageType != WebSocketMessageType.Close);
    }
}
