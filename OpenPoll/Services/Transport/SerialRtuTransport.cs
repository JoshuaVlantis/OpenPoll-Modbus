using System;
using System.IO.Ports;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Modbus RTU over a serial port. Frame is <c>SlaveId · PDU · CRC16-MODBUS (low,high)</c>.
/// Reads bytes until a 3.5-character inter-byte idle elapses (signalled by SerialPort's
/// <see cref="System.IO.Ports.SerialPort.ReadTimeout"/>).
/// </summary>
public sealed class SerialRtuTransport : IModbusTransport
{
    private readonly SerialPort _port;

    public SerialRtuTransport(SerialPort openPort)
    {
        _port = openPort;
        // 3.5 char idle at the configured baud (clamped to 20ms minimum so OS scheduling jitter
        // doesn't truncate frames).
        int idleMs = Math.Max(20, (int)Math.Ceiling(3500.0 * 11 / Math.Max(1200, _port.BaudRate)));
        _port.ReadTimeout = idleMs;
    }

    public bool Connected => _port.IsOpen;
    public int ReadTimeoutMs { get => _port.ReadTimeout; set => _port.ReadTimeout = value; }
    public int WriteTimeoutMs { get => _port.WriteTimeout; set => _port.WriteTimeout = value; }

    public byte[] SendReceive(byte unitId, byte[] pdu)
    {
        var frame = ModbusCrc.WrapRtu(unitId, pdu);
        _port.DiscardInBuffer();
        _port.Write(frame, 0, frame.Length);

        // Receive: block on the first byte (use a longer one-shot timeout for the response wait),
        // then accumulate bytes until inter-byte timeout.
        int firstByteTimeout = Math.Max(_port.ReadTimeout, 1000);
        var saved = _port.ReadTimeout;
        _port.ReadTimeout = firstByteTimeout;
        var buf = new byte[256];
        int len = 0;
        try { buf[len++] = (byte)_port.ReadByte(); }
        finally { _port.ReadTimeout = saved; }

        while (len < buf.Length)
        {
            try { buf[len++] = (byte)_port.ReadByte(); }
            catch (TimeoutException) { break; }
        }

        if (len < 4 || !ModbusCrc.Verify(buf, 0, len))
            throw new System.IO.IOException("CRC mismatch on RTU response");
        if (buf[0] != unitId && unitId != 0)
            throw new System.IO.IOException($"Unexpected slave id {buf[0]:X2}");

        var resp = new byte[len - 3];
        Buffer.BlockCopy(buf, 1, resp, 0, resp.Length);
        return resp;
    }

    public void Dispose() { try { _port.Close(); } catch { } }
}
