namespace OpenPoll.Models;

public enum ConnectionMode
{
    /// <summary>Modbus TCP — MBAP header + PDU over a TCP stream (default port 502).</summary>
    Tcp = 0,
    /// <summary>Modbus RTU — SlaveId + PDU + CRC-16 over a serial port.</summary>
    Serial = 1,
    /// <summary>Modbus over UDP — same MBAP+PDU framing as TCP, one frame per datagram.</summary>
    Udp = 2,
    /// <summary>Modbus RTU framing (SlaveId + PDU + CRC) carried over a TCP socket — gateways use this.</summary>
    RtuOverTcp = 3,
    /// <summary>Modbus ASCII — hex-encoded PDU framed by <c>:</c> and CRLF, LRC checksum, over TCP.</summary>
    AsciiOverTcp = 4,
    /// <summary>Modbus TCP secured with TLS (RFC 9300). Same MBAP framing inside an SslStream.</summary>
    TcpTls = 5,
    /// <summary>Modbus ASCII over a serial port (RS-232/485).</summary>
    AsciiOverSerial = 6,
    /// <summary>Modbus RTU framing carried inside UDP datagrams.</summary>
    RtuOverUdp = 7,
    /// <summary>Modbus ASCII framing carried inside UDP datagrams.</summary>
    AsciiOverUdp = 8,
}
