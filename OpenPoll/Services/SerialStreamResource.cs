using System.IO.Ports;
using NModbus.IO;

namespace OpenPoll.Services;

/// <summary>
/// Adapts a <see cref="System.IO.Ports.SerialPort"/> to NModbus's <see cref="IStreamResource"/>.
/// NModbus 3.x dropped its bundled SerialPortAdapter so consumers wire their own.
/// </summary>
internal sealed class SerialStreamResource : IStreamResource
{
    private readonly SerialPort _port;

    public SerialStreamResource(SerialPort port) => _port = port;

    public int InfiniteTimeout => SerialPort.InfiniteTimeout;
    public int ReadTimeout
    {
        get => _port.ReadTimeout;
        set => _port.ReadTimeout = value;
    }
    public int WriteTimeout
    {
        get => _port.WriteTimeout;
        set => _port.WriteTimeout = value;
    }

    public void DiscardInBuffer() => _port.DiscardInBuffer();
    public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);
    public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);

    public void Dispose()
    {
        try { _port.Close(); } catch { }
        _port.Dispose();
    }
}
