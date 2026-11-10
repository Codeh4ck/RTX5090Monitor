using System.Net.WebSockets;

namespace RTXLens.UI.Tests.TestDoubles;

internal sealed record SocketPacket(byte[] Bytes, WebSocketMessageType Type, bool EndOfMessage = true);
