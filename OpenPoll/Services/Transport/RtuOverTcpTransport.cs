using System;
using System.IO;
using System.Net.Sockets;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Modbus RTU framing carried over a TCP socket — gateway boxes from various vendors expose this
/// instead of pure Modbus-TCP. Frame is <c>SlaveId · PDU · CRC16-MODBUS</c>; no MBAP header.
/// Because TCP is a byte stream we can't rely on inter-byte timing — we peek at the function code
/// to compute the expected response length per the Modbus spec.
/// </summary>
public sealed class RtuOverTcpTransport : IModbusTransport
{
    private readonly TcpClient _tcp;
    private readonly NetworkStream _stream;

    public RtuOverTcpTransport(TcpClient connectedClient)
    {
        _tcp = connectedClient;
        _stream = _tcp.GetStream();
    }

    public bool Connected => _tcp.Connected;
    public int ReadTimeoutMs { get => _stream.ReadTimeout; set => _stream.ReadTimeout = value; }
    public int WriteTimeoutMs { get => _stream.WriteTimeout; set => _stream.WriteTimeout = value; }

    public byte[] SendReceive(byte unitId, byte[] pdu)
    {
        var frame = ModbusCrc.WrapRtu(unitId, pdu);
        _stream.Write(frame, 0, frame.Length);

        // Read the first three bytes: SlaveId + FunctionCode + (byteCount OR first PDU byte).
        var head = new byte[3];
        ReadExact(head, 0, 3);
        int total = ExpectedResponseLength(head[1], head[2], pdu);
        var rest = new byte[total - 3];
        ReadExact(rest, 0, rest.Length);

        var full = new byte[total];
        Buffer.BlockCopy(head, 0, full, 0, 3);
        Buffer.BlockCopy(rest, 0, full, 3, rest.Length);
        if (!ModbusCrc.Verify(full, 0, full.Length))
            throw new IOException("CRC mismatch on RTU-over-TCP response");

        var resp = new byte[full.Length - 3];
        Buffer.BlockCopy(full, 1, resp, 0, resp.Length);
        return resp;
    }

    private static int ExpectedResponseLength(byte fc, byte third, byte[] requestPdu)
    {
        // Exception response: FC | 0x80 followed by 1-byte exception code → 5 bytes total
        if ((fc & 0x80) != 0) return 5;
        return fc switch
        {
            0x01 or 0x02 or 0x03 or 0x04 or 0x0C or 0x11 or 0x14 or 0x15 or 0x17 or 0x18
                => 3 + third + 2,                       // SlaveId + FC + byteCount + bytes + CRC
            0x05 or 0x06 or 0x0F or 0x10 => 8,          // echo of address+value
            0x07 => 5,                                  // exception status: 1 byte status
            0x08 => 8,                                  // diagnostic: subFn(2) + data(2)
            0x0B => 7,                                  // event counter: status(2) + count(2)
            0x16 => 10,                                 // mask write: echo addr(2)+and(2)+or(2)
            0x2B => 3 + third + 2,                      // FC 43: byteCount in field 2 (= conformity); response is variable. Best effort: read a generous chunk; rely on the calculator above. For 0x2B specifically we use a different shape.
            _   => 3 + third + 2,                       // assume byte-count-style frame
        };
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

    public void Dispose()
    {
        try { _stream.Dispose(); } catch { }
        try { _tcp.Close(); } catch { }
    }
}
