using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Modbus ASCII — printable hex framing over a TCP (or serial) link. Each frame is
/// <c>:AABBCC…XXLLrn</c> where each byte of the payload is encoded as two ASCII hex
/// characters and <c>LL</c> is the LRC (longitudinal redundancy check, 2's complement of the
/// byte sum). Less efficient than RTU but human-readable and tolerates intermittent links well.
/// </summary>
public sealed class AsciiOverTcpTransport : IModbusTransport
{
    private readonly TcpClient _tcp;
    private readonly NetworkStream _stream;

    public AsciiOverTcpTransport(TcpClient connectedClient)
    {
        _tcp = connectedClient;
        _stream = _tcp.GetStream();
    }

    public bool Connected => _tcp.Connected;
    public int ReadTimeoutMs { get => _stream.ReadTimeout; set => _stream.ReadTimeout = value; }
    public int WriteTimeoutMs { get => _stream.WriteTimeout; set => _stream.WriteTimeout = value; }

    public byte[] SendReceive(byte unitId, byte[] pdu)
    {
        var ascii = WrapAscii(unitId, pdu);
        _stream.Write(ascii, 0, ascii.Length);

        // Read until we see CRLF.
        var buf = new byte[512];
        int len = 0;
        while (true)
        {
            int b = _stream.ReadByte();
            if (b < 0) throw new IOException("ASCII stream closed mid-frame");
            buf[len++] = (byte)b;
            if (len >= 2 && buf[len - 2] == '\r' && buf[len - 1] == '\n') break;
            if (len == buf.Length) throw new IOException("ASCII frame too long");
        }

        return UnwrapAscii(buf, len, expectedUnitId: unitId);
    }

    public static byte[] WrapAscii(byte unitId, byte[] pdu)
    {
        byte sum = unitId;
        foreach (var b in pdu) sum += b;
        byte lrc = (byte)(-(sbyte)sum);

        var sb = new StringBuilder(":");
        sb.Append(unitId.ToString("X2"));
        foreach (var b in pdu) sb.Append(b.ToString("X2"));
        sb.Append(lrc.ToString("X2"));
        sb.Append("\r\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    public static byte[] UnwrapAscii(byte[] buf, int len, byte expectedUnitId)
    {
        if (len < 7 || buf[0] != ':' || buf[len - 2] != '\r' || buf[len - 1] != '\n')
            throw new IOException("Malformed ASCII frame");
        var hex = Encoding.ASCII.GetString(buf, 1, len - 3);   // strip ':' and CRLF
        if (hex.Length % 2 != 0) throw new IOException("Odd-length ASCII payload");

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

        byte slaveId = bytes[0];
        byte receivedLrc = bytes[^1];
        byte sum = 0;
        for (int i = 0; i < bytes.Length - 1; i++) sum += bytes[i];
        byte expectedLrc = (byte)(-(sbyte)sum);
        if (receivedLrc != expectedLrc)
            throw new IOException($"ASCII LRC mismatch: got {receivedLrc:X2}, expected {expectedLrc:X2}");

        if (slaveId != expectedUnitId && expectedUnitId != 0)
            throw new IOException($"Unexpected slave id {slaveId:X2}");

        var pdu = new byte[bytes.Length - 2];   // strip slave id and LRC
        Buffer.BlockCopy(bytes, 1, pdu, 0, pdu.Length);
        return pdu;
    }

    public void Dispose()
    {
        try { _stream.Dispose(); } catch { }
        try { _tcp.Close(); } catch { }
    }
}
