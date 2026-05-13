using System;
using System.Net;
using System.Net.Sockets;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Modbus ASCII framing carried over UDP datagrams. One <c>:AABB…\r\n</c> frame per datagram.
/// </summary>
public sealed class AsciiOverUdpTransport : IModbusTransport
{
    private readonly UdpClient _udp;
    private readonly IPEndPoint _remote;

    public AsciiOverUdpTransport(string host, int port, int readTimeoutMs)
    {
        _udp = new UdpClient();
        _udp.Client.ReceiveTimeout = readTimeoutMs;
        _udp.Client.SendTimeout = readTimeoutMs;
        _remote = new IPEndPoint(Dns.GetHostAddresses(host)[0], port);
    }

    public bool Connected => true;
    public int ReadTimeoutMs { get => _udp.Client.ReceiveTimeout; set => _udp.Client.ReceiveTimeout = value; }
    public int WriteTimeoutMs { get => _udp.Client.SendTimeout; set => _udp.Client.SendTimeout = value; }

    public byte[] SendReceive(byte unitId, byte[] pdu)
    {
        var frame = AsciiOverTcpTransport.WrapAscii(unitId, pdu);
        _udp.Send(frame, frame.Length, _remote);

        IPEndPoint? sender = null;
        var resp = _udp.Receive(ref sender);
        return AsciiOverTcpTransport.UnwrapAscii(resp, resp.Length, expectedUnitId: unitId);
    }

    public void Dispose() { try { _udp.Close(); } catch { } }
}
