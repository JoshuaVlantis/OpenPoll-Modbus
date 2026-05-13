using System;
using System.IO;
using System.IO.Ports;
using System.Text;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Modbus ASCII over a serial line. Same framing as <see cref="AsciiOverTcpTransport"/> —
/// <c>:</c> + hex bytes + LRC + CRLF — over <see cref="SerialPort"/> instead of TCP.
/// </summary>
public sealed class AsciiOverSerialTransport : IModbusTransport
{
    private readonly SerialPort _port;

    public AsciiOverSerialTransport(SerialPort openPort)
    {
        _port = openPort;
        // ASCII tolerates slow links — give the read path a generous one-second budget by default;
        // the caller may override via ReadTimeoutMs after construction.
        _port.ReadTimeout = Math.Max(_port.ReadTimeout, 1000);
        _port.NewLine = "\r\n";
    }

    public bool Connected => _port.IsOpen;
    public int ReadTimeoutMs { get => _port.ReadTimeout; set => _port.ReadTimeout = value; }
    public int WriteTimeoutMs { get => _port.WriteTimeout; set => _port.WriteTimeout = value; }

    public byte[] SendReceive(byte unitId, byte[] pdu)
    {
        var frame = AsciiOverTcpTransport.WrapAscii(unitId, pdu);
        _port.DiscardInBuffer();
        _port.Write(frame, 0, frame.Length);

        // Read until CRLF — SerialPort.ReadLine strips the terminator, so we manually append it.
        string body;
        try { body = _port.ReadLine(); }
        catch (TimeoutException) { throw new IOException("ASCII serial read timed out"); }
        var asciiBytes = Encoding.ASCII.GetBytes(body + "\r\n");
        return AsciiOverTcpTransport.UnwrapAscii(asciiBytes, asciiBytes.Length, expectedUnitId: unitId);
    }

    public void Dispose() { try { _port.Close(); } catch { } }
}
