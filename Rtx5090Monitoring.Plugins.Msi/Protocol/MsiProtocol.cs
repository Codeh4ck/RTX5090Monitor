using System.Text;

namespace Rtx5090Monitoring.Plugins.Msi.Protocol;

internal static class MsiProtocol
{
    public static byte[] ReadRequest(byte register) => register is not (0x10 or 0x11 or 0x12 or 0x30 or 0x40 or 0x41 or 0x42 or 0xE0)
        ? throw new ArgumentOutOfRangeException(nameof(register))
        : Packet(0x51, register, []);

    public static byte[] Handshake() => Packet(0xFA, 0x51, []);

    public static byte[] FanRequest(bool automatic, int duty) => duty is < 0 or > 100
        ? throw new ArgumentOutOfRangeException(nameof(duty))
        : Packet(0x50, 0x41, [(byte)(automatic ? 1 : 3), 0, (byte)(automatic ? 0 : duty)]);

    private static byte[] Packet(byte opcode, byte register, ReadOnlySpan<byte> data)
    {
        var packet = new byte[65];
        packet[1] = opcode;
        packet[2] = register;
        data.CopyTo(packet.AsSpan(3));
        return packet;
    }

    public static byte[]? MatchReply(byte[] request, ReadOnlySpan<byte> raw, int minimumPayload)
    {
        if (raw.Length > 1 && raw[0] == 0)
        {
            raw = raw[1..];
        }

        if (raw.Length < 2 || raw[0] != request[1])
        {
            return null;
        }

        if (raw[1] == 0xFE)
        {
            throw new IOException("The PSU rejected this command.");
        }

        if (raw[1] != request[2])
        {
            return null;
        }

        if (raw.Length < minimumPayload + 2)
        {
            throw new IOException("The PSU returned a truncated report.");
        }

        if (raw[2] == 0xFE && (request[1] == 0xFA || raw[3..].IndexOfAnyExcept((byte)0) < 0))
        {
            throw new IOException("The PSU is busy.");
        }

        var reply = new byte[65];
        raw[..Math.Min(raw.Length, 64)].CopyTo(reply.AsSpan(1));

        return reply;
    }

    public static double Linear11(byte[] reply, int offset)
    {
        var word = (ushort)(reply[offset] | (reply[offset + 1] << 8));
        return (word & 0x7FF) * Math.Pow(2, (short)word >> 11);
    }

    public static string Text(byte[] reply) => Encoding.ASCII.GetString(reply.AsSpan(3)).TrimEnd('\0', ' ');

    public static double[][] Currents(byte[] reply) =>
    [
        .. Enumerable.Range(0, 2)
            .Select(connector => Enumerable.Range(0, 6).Select(pin => Linear11(reply, 9 + (connector * 12) + (pin * 2))).ToArray())
    ];
}