using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Modbus over UDP — same MBAP+PDU framing as TCP, one datagram per request/response. No
/// connection state, so this "transport" is really a (host, port) tuple plus a socket.
/// </summary>
public sealed class UdpMbapTransport : IModbusTransport
{
    private readonly UdpClient _udp;
    private readonly IPEndPoint _remote;
    private int _txn;

    public UdpMbapTransport(string host, int port, int readTimeoutMs)
    {
        _udp = new UdpClient();
        _udp.Client.ReceiveTimeout = readTimeoutMs;
        _udp.Client.SendTimeout = readTimeoutMs;
        _remote = new IPEndPoint(Dns.GetHostAddresses(host)[0], port);
    }

    public bool Connected => true;   // UDP is connectionless; "open" once the socket exists.
    public int ReadTimeoutMs { get => _udp.Client.ReceiveTimeout; set => _udp.Client.ReceiveTimeout = value; }
    public int WriteTimeoutMs { get => _udp.Client.SendTimeout; set => _udp.Client.SendTimeout = value; }

    public byte[] SendReceive(byte unitId, byte[] pdu)
    {
        ushort txn = (ushort)Interlocked.Increment(ref _txn);
        var frame = TcpMbapTransport.WrapMbap(txn, unitId, pdu);
        _udp.Send(frame, frame.Length, _remote);

        IPEndPoint? sender = null;
        var resp = _udp.Receive(ref sender);
        if (resp.Length < 7) throw new System.IO.IOException("Short MBAP response");
        int length = (resp[4] << 8) | resp[5];
        if (length < 2 || resp.Length < 7 + length - 1) throw new System.IO.IOException("Bad MBAP length");
        var body = new byte[length - 1];
        Buffer.BlockCopy(resp, 7, body, 0, body.Length);
        return body;
    }

    public void Dispose() { try { _udp.Close(); } catch { } }
}
