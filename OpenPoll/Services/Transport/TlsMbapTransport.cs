using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Modbus TCP wrapped in TLS (RFC 9300). Same MBAP+PDU framing as <see cref="TcpMbapTransport"/>,
/// just over an <see cref="SslStream"/>. By default we accept ANY server certificate — Modbus
/// engineers test with self-signed certs more often than not. Set <see cref="ValidateServerCert"/>
/// to enable proper validation in production deployments.
/// </summary>
public sealed class TlsMbapTransport : IModbusTransport
{
    private readonly TcpClient _tcp;
    private readonly SslStream _ssl;
    private int _txn;

    public bool ValidateServerCert { get; }

    public TlsMbapTransport(string host, int port, bool validateCert, int handshakeTimeoutMs)
    {
        ValidateServerCert = validateCert;
        _tcp = new TcpClient();
        var connect = _tcp.BeginConnect(host, port, null, null);
        if (!connect.AsyncWaitHandle.WaitOne(Math.Max(500, handshakeTimeoutMs)))
        {
            _tcp.Close();
            throw new IOException($"TLS connect to {host}:{port} timed out");
        }
        _tcp.EndConnect(connect);

        _ssl = new SslStream(_tcp.GetStream(), leaveInnerStreamOpen: false,
            userCertificateValidationCallback: AcceptCert);
        _ssl.AuthenticateAsClient(new SslClientAuthenticationOptions
        {
            TargetHost = host,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        });
    }

    private bool AcceptCert(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
        => !ValidateServerCert || errors == SslPolicyErrors.None;

    public bool Connected => _tcp.Connected;
    public int ReadTimeoutMs { get => _ssl.ReadTimeout; set => _ssl.ReadTimeout = value; }
    public int WriteTimeoutMs { get => _ssl.WriteTimeout; set => _ssl.WriteTimeout = value; }

    public byte[] SendReceive(byte unitId, byte[] pdu)
    {
        ushort txn = (ushort)Interlocked.Increment(ref _txn);
        var frame = TcpMbapTransport.WrapMbap(txn, unitId, pdu);
        _ssl.Write(frame, 0, frame.Length);

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
            int read = _ssl.Read(buf, off + total, n - total);
            if (read == 0) throw new IOException("TLS stream closed before response complete");
            total += read;
        }
    }

    public void Dispose()
    {
        try { _ssl.Dispose(); } catch { }
        try { _tcp.Close(); } catch { }
    }
}
