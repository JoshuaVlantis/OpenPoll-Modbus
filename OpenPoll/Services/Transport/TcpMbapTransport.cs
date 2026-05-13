using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Modbus over TCP — MBAP (7 bytes: transaction id, protocol id=0, length, unit id) + PDU.
/// One transaction id per <see cref="SendReceive"/>; the next response must echo it back.
/// </summary>
public sealed class TcpMbapTransport : IModbusTransport
{
    private readonly TcpClient _tcp;
    private readonly NetworkStream _stream;
    private int _txn;

    public TcpMbapTransport(TcpClient connectedClient)
    {
        _tcp = connectedClient;
        _stream = _tcp.GetStream();
    }

    public bool Connected => _tcp.Connected;
    public int ReadTimeoutMs { get => _stream.ReadTimeout; set => _stream.ReadTimeout = value; }
    public int WriteTimeoutMs { get => _stream.WriteTimeout; set => _stream.WriteTimeout = value; }

    public byte[] SendReceive(byte unitId, byte[] pdu)
    {
        ushort txn = (ushort)Interlocked.Increment(ref _txn);
        var frame = WrapMbap(txn, unitId, pdu);
        _stream.Write(frame, 0, frame.Length);

        var header = new byte[7];
        ReadExact(header, 0, 7);
        int length = (header[4] << 8) | header[5];
        if (length < 2 || length > 254) throw new IOException($"Bad MBAP length {length}");
        var body = new byte[length - 1];
        ReadExact(body, 0, body.Length);
        return body;
    }

    private void ReadExact(byte[] buf, int off, int n)
    {
        int total = 0;
        while (total < n)
        {
            int read = _stream.Read(buf, off + total, n - total);
            if (read == 0) throw new IOException("Stream closed before response complete");
            total += read;
        }
    }

    public static byte[] WrapMbap(ushort txn, byte unitId, byte[] pdu)
    {
        int length = pdu.Length + 1;
        var frame = new byte[7 + pdu.Length];
        frame[0] = (byte)(txn >> 8); frame[1] = (byte)txn;
        frame[2] = 0; frame[3] = 0;
        frame[4] = (byte)(length >> 8); frame[5] = (byte)length;
        frame[6] = unitId;
        Buffer.BlockCopy(pdu, 0, frame, 7, pdu.Length);
        return frame;
    }

    public void Dispose()
    {
        try { _stream.Dispose(); } catch { }
        try { _tcp.Close(); } catch { }
    }
}
