using System;
using System.Net;
using System.Net.Sockets;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Modbus RTU framing over UDP datagrams. Each datagram carries
/// <c>SlaveId · PDU · CRC16</c> — no MBAP header. Mostly used to bridge serial-style devices
/// over a routed network without a gateway.
/// </summary>
public sealed class RtuOverUdpTransport : IModbusTransport
{
    private readonly UdpClient _udp;
    private readonly IPEndPoint _remote;

    public RtuOverUdpTransport(string host, int port, int readTimeoutMs)
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
        var frame = ModbusCrc.WrapRtu(unitId, pdu);
        _udp.Send(frame, frame.Length, _remote);

        IPEndPoint? sender = null;
        var resp = _udp.Receive(ref sender);
        if (resp.Length < 4 || !ModbusCrc.Verify(resp, 0, resp.Length))
            throw new System.IO.IOException("CRC mismatch on RTU-over-UDP response");
        if (resp[0] != unitId && unitId != 0)
            throw new System.IO.IOException($"Unexpected slave id {resp[0]:X2}");
        var pduResp = new byte[resp.Length - 3];
        Buffer.BlockCopy(resp, 1, pduResp, 0, pduResp.Length);
        return pduResp;
    }

    public void Dispose() { try { _udp.Close(); } catch { } }
}
