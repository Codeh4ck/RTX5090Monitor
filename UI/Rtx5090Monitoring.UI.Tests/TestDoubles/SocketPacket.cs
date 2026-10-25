using System.Net.WebSockets;

namespace Rtx5090Monitoring.UI.Tests.TestDoubles;

internal sealed record SocketPacket(byte[] Bytes, WebSocketMessageType Type, bool EndOfMessage = true);
