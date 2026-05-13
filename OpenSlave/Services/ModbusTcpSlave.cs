using System;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSlave.Services;

/// <summary>
/// Minimal in-process Modbus TCP slave.
/// Implements function codes 01, 02, 03, 04, 05, 06, 15, 16, 22, 23 plus
/// response-delay / skip-response / forced-exception-06 fault simulation.
///
/// Protocol surface:
///   MBAP (7 bytes): Transaction ID, Protocol ID = 0, Length, Unit ID
///   PDU         : Function code, payload
///
/// Owns four 65536-element data tables; callers (UI, CLI) seed and observe them.
/// </summary>
public sealed class ModbusTcpSlave : IDisposable
{
    public const int TableSize = 65536;

    private const int MbapHeaderLength = 7;
    private const int MaxPduLength = 253;
    private const int MaxAduLength = MbapHeaderLength + MaxPduLength;

    private readonly object _gate = new();
    private readonly Random _rng = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private int _connectedClients;
    private int _commEventCounter;       // FC 11 / FC 08 sub 0x0B+0x0E (bus + slave message count)
    private int _commErrorCounter;       // FC 08 sub 0x0C — bus communication errors (CRC/LRC fails)
    private int _exceptionCounter;       // FC 08 sub 0x0D — exception responses we've sent
    private int _slaveNoResponseCounter; // FC 08 sub 0x0F — skipped/broadcast responses
    private int _slaveBusyCounter;       // FC 08 sub 0x11 — exception-06 responses
    private ushort _diagRegister;        // FC 08 sub 0x02 — bit register, vendor-defined

    private SerialPort? _serial;
    private CancellationTokenSource? _serialCts;
    private Task? _serialLoop;
    public bool SerialRunning => _serial?.IsOpen ?? false;

    private UdpClient? _udp;
    private CancellationTokenSource? _udpCts;
    private Task? _udpLoop;
    public bool UdpRunning => _udp is not null;

    private TcpListener? _rtuTcpListener;
    private CancellationTokenSource? _rtuTcpCts;
    private Task? _rtuTcpLoop;
    public bool RtuOverTcpRunning => _rtuTcpListener is not null;

    private TcpListener? _asciiListener;
    private CancellationTokenSource? _asciiCts;
    private Task? _asciiLoop;
    public bool AsciiOverTcpRunning => _asciiListener is not null;

    private TcpListener? _tlsListener;
    private X509Certificate2? _tlsCert;
    private CancellationTokenSource? _tlsCts;
    private Task? _tlsLoop;
    public bool TlsRunning => _tlsListener is not null;

    private SerialPort? _asciiSerial;
    private CancellationTokenSource? _asciiSerialCts;
    private Task? _asciiSerialLoop;
    public bool AsciiSerialRunning => _asciiSerial?.IsOpen ?? false;

    private UdpClient? _udpRtu;
    private CancellationTokenSource? _udpRtuCts;
    private Task? _udpRtuLoop;
    public bool RtuOverUdpRunning => _udpRtu is not null;

    private UdpClient? _udpAscii;
    private CancellationTokenSource? _udpAsciiCts;
    private Task? _udpAsciiLoop;
    public bool AsciiOverUdpRunning => _udpAscii is not null;

    public bool[] Coils { get; } = new bool[TableSize];
    public bool[] DiscreteInputs { get; } = new bool[TableSize];
    public ushort[] HoldingRegisters { get; } = new ushort[TableSize];
    public ushort[] InputRegisters { get; } = new ushort[TableSize];

    public byte SlaveId { get; set; } = 1;
    public bool IgnoreUnitId { get; set; }
    public int ResponseDelayMs { get; set; }
    public bool SkipResponses { get; set; }
    public bool ReturnExceptionBusy { get; set; }

    /// <summary>
    /// FC 43 / MEI 0x0E device identification objects. Slot 0..6 follow the Modbus spec; users may
    /// override any of them. Empty strings are reported but trimmed by the response builder.
    /// </summary>
    public DeviceIdentificationTable DeviceIdentification { get; } = new();

    public int ConnectedClients => Volatile.Read(ref _connectedClients);
    public bool IsRunning { get; private set; }

    public event Action<int, int>? CoilsChanged;
    public event Action<int, int>? HoldingRegistersChanged;
    public event Action<int>? ConnectedClientsChanged;

    /// <summary>Raised after every successfully-handled request so callers can log traffic.</summary>
    public event Action<RequestEvent>? RequestHandled;

    public void Start(int port)
    {
        if (IsRunning) throw new InvalidOperationException("Slave already running.");
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        IsRunning = true;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _listener = null;
        _cts?.Dispose();
        _cts = null;
        _acceptLoop = null;
        Interlocked.Exchange(ref _connectedClients, 0);
        ConnectedClientsChanged?.Invoke(0);
    }

    /// <summary>
    /// Open <paramref name="portName"/> and start servicing Modbus-RTU requests on it. Coexists
    /// with the TCP listener — call <see cref="Start"/> too if you want both transports live at
    /// once. The dispatcher and data tables are shared.
    /// </summary>
    public void StartSerial(string portName, int baudRate = 9600, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
    {
        if (SerialRunning) throw new InvalidOperationException("Serial slave already running.");
        _serial = new SerialPort(portName, baudRate, parity, 8, stopBits)
        {
            // Inter-byte timeout: 3.5 character times per the Modbus RTU spec. At 9600 baud this is
            // ~4ms; clamp to 20ms minimum so the OS can deliver bytes reliably.
            ReadTimeout = Math.Max(20, (int)Math.Ceiling(3500.0 * 11 / baudRate)),
            WriteTimeout = 1000,
        };
        _serial.Open();
        _serialCts = new CancellationTokenSource();
        var ct = _serialCts.Token;
        _serialLoop = Task.Run(() => SerialReadLoop(ct));
    }

    public void StopSerial()
    {
        if (!SerialRunning) return;
        try { _serialCts?.Cancel(); } catch { }
        try { _serial?.Close(); } catch { }
        try { _serialLoop?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _serial = null;
        _serialCts?.Dispose();
        _serialCts = null;
        _serialLoop = null;
    }

    private void SerialReadLoop(CancellationToken ct)
    {
        var port = _serial!;
        var buffer = new byte[MaxAduLength + 4];   // headroom for slave id + CRC
        while (!ct.IsCancellationRequested && port.IsOpen)
        {
            int len = 0;
            try
            {
                int first = port.ReadByte();           // block until first byte (slave id)
                if (first < 0) continue;
                buffer[len++] = (byte)first;
                while (len < buffer.Length)
                {
                    try { buffer[len++] = (byte)port.ReadByte(); }
                    catch (TimeoutException) { break; }   // inter-byte idle ⇒ frame complete
                }
            }
            catch (TimeoutException) { continue; }
            catch (OperationCanceledException) { return; }
            catch { return; }

            if (len < 4) continue;                       // SlaveId + FC + CRC = 4 min
            if (!ModbusCrc.Verify(buffer, 0, len)) continue;

            byte unitId = buffer[0];
            if (!IgnoreUnitId && unitId != SlaveId && unitId != 0) continue;

            int pduLen = len - 3;                        // strip SlaveId + CRC
            byte[] response;
            try
            {
                if (SkipResponses && _rng.Next(10) == 0) continue;
                response = ReturnExceptionBusy
                    ? BuildException(buffer[1], 0x06)
                    : Dispatch(buffer, 1, pduLen);
            }
            catch { continue; }

            if (ResponseDelayMs > 0) Thread.Sleep(ResponseDelayMs);
            if (unitId == 0) continue;                   // broadcast: no response per spec

            var frame = ModbusCrc.WrapRtu(unitId, response);
            try { port.Write(frame, 0, frame.Length); } catch { return; }
        }
    }

    /// <summary>
    /// Listen for Modbus-over-UDP datagrams on <paramref name="port"/>. UDP framing is the same
    /// MBAP+PDU as TCP, just one frame per datagram. Coexists with TCP and serial.
    /// </summary>
    public void StartUdp(int port)
    {
        if (UdpRunning) throw new InvalidOperationException("UDP slave already running.");
        _udp = new UdpClient(port);
        _udpCts = new CancellationTokenSource();
        var ct = _udpCts.Token;
        _udpLoop = Task.Run(() => UdpLoopAsync(ct));
    }

    public void StopUdp()
    {
        if (!UdpRunning) return;
        try { _udpCts?.Cancel(); } catch { }
        try { _udp?.Close(); } catch { }
        try { _udpLoop?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _udp = null;
        _udpCts?.Dispose();
        _udpCts = null;
        _udpLoop = null;
    }

    private async Task UdpLoopAsync(CancellationToken ct)
    {
        var udp = _udp!;
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult datagram;
            try { datagram = await udp.ReceiveAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch { continue; }

            var buf = datagram.Buffer;
            if (buf.Length < MbapHeaderLength + 1) continue;

            int protocolId = (buf[2] << 8) | buf[3];
            int length     = (buf[4] << 8) | buf[5];
            byte unitId    = buf[6];
            if (protocolId != 0 || length < 2 || length > MaxPduLength + 1) continue;
            int pduLen = length - 1;
            if (buf.Length < MbapHeaderLength + pduLen) continue;

            if (!IgnoreUnitId && unitId != SlaveId && unitId != 0) continue;
            if (SkipResponses && _rng.Next(10) == 0) continue;

            byte[] resp;
            try { resp = ReturnExceptionBusy ? BuildException(buf[MbapHeaderLength], 0x06)
                                             : Dispatch(buf, MbapHeaderLength, pduLen); }
            catch { continue; }

            if (ResponseDelayMs > 0)
            {
                try { await Task.Delay(ResponseDelayMs, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }

            int frameLen = resp.Length + 1;
            var outFrame = new byte[MbapHeaderLength + resp.Length];
            outFrame[0] = buf[0]; outFrame[1] = buf[1];   // txn id echo
            outFrame[2] = 0; outFrame[3] = 0;
            outFrame[4] = (byte)(frameLen >> 8);
            outFrame[5] = (byte)frameLen;
            outFrame[6] = unitId;
            Buffer.BlockCopy(resp, 0, outFrame, MbapHeaderLength, resp.Length);

            try { await udp.SendAsync(outFrame, outFrame.Length, datagram.RemoteEndPoint).ConfigureAwait(false); }
            catch { /* best effort */ }
        }
    }

    /// <summary>Listen for Modbus RTU frames carried over a TCP stream (gateway-style). No MBAP header.</summary>
    public void StartRtuOverTcp(int port)
    {
        if (RtuOverTcpRunning) throw new InvalidOperationException("RTU-over-TCP slave already running.");
        _rtuTcpListener = new TcpListener(IPAddress.Any, port);
        _rtuTcpListener.Start();
        _rtuTcpCts = new CancellationTokenSource();
        var ct = _rtuTcpCts.Token;
        _rtuTcpLoop = Task.Run(() => RtuOverTcpAcceptLoopAsync(ct));
    }

    public void StopRtuOverTcp()
    {
        if (!RtuOverTcpRunning) return;
        try { _rtuTcpCts?.Cancel(); } catch { }
        try { _rtuTcpListener?.Stop(); } catch { }
        try { _rtuTcpLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _rtuTcpListener = null;
        _rtuTcpCts?.Dispose(); _rtuTcpCts = null; _rtuTcpLoop = null;
    }

    private async Task RtuOverTcpAcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _rtuTcpListener!.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch { return; }
            _ = Task.Run(() => HandleRtuOverTcpClientAsync(client, ct), ct);
        }
    }

    private async Task HandleRtuOverTcpClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            client.NoDelay = true;
            using var stream = client.GetStream();
            var buffer = new byte[260];

            while (!ct.IsCancellationRequested)
            {
                // Read SlaveId + FunctionCode (2 bytes).
                if (!await ReadExactAsync(stream, buffer, 0, 2, ct).ConfigureAwait(false)) return;
                byte fc = buffer[1];
                int totalLen = await DetermineRtuRequestLengthAsync(stream, buffer, fc, ct).ConfigureAwait(false);
                if (totalLen <= 0 || totalLen > buffer.Length) return;
                if (!ModbusCrc.Verify(buffer, 0, totalLen)) continue;

                byte unitId = buffer[0];
                if (!IgnoreUnitId && unitId != SlaveId && unitId != 0) continue;
                if (SkipResponses && _rng.Next(10) == 0) continue;

                int pduLen = totalLen - 3;   // strip SlaveId + CRC(2)
                byte[] resp;
                try { resp = ReturnExceptionBusy ? BuildException(fc, 0x06) : Dispatch(buffer, 1, pduLen); }
                catch { continue; }

                if (ResponseDelayMs > 0)
                {
                    try { await Task.Delay(ResponseDelayMs, ct).ConfigureAwait(false); }
                    catch { return; }
                }
                if (unitId == 0) continue;

                var frame = ModbusCrc.WrapRtu(unitId, resp);
                try { await stream.WriteAsync(frame, ct).ConfigureAwait(false); } catch { return; }
            }
        }
        catch { /* connection-lifetime best-effort */ }
        finally { try { client.Close(); } catch { } }
    }

    /// <summary>
    /// Pull additional bytes from the stream to complete a request frame, returning the total
    /// frame length (SlaveId + PDU + CRC). For fixed-shape FCs the length is known up front; for
    /// FC15/FC16/FC23 we peek the byteCount field and extend.
    /// </summary>
    private async Task<int> DetermineRtuRequestLengthAsync(NetworkStream stream, byte[] buf, byte fc, CancellationToken ct)
    {
        switch (fc)
        {
            case 0x01: case 0x02: case 0x03: case 0x04: case 0x05: case 0x06:
                return await ReadExactAsync(stream, buf, 2, 6, ct).ConfigureAwait(false) ? 8 : -1;
            case 0x07: case 0x0B: case 0x0C: case 0x11:
                return await ReadExactAsync(stream, buf, 2, 2, ct).ConfigureAwait(false) ? 4 : -1;
            case 0x08:
                return await ReadExactAsync(stream, buf, 2, 6, ct).ConfigureAwait(false) ? 8 : -1;
            case 0x16:
                return await ReadExactAsync(stream, buf, 2, 8, ct).ConfigureAwait(false) ? 10 : -1;
            case 0x2B:
                return await ReadExactAsync(stream, buf, 2, 4, ct).ConfigureAwait(false) ? 6 : -1;
            case 0x0F: case 0x10:
                // Read addr(2) + qty(2) + byteCount(1). Then byteCount payload bytes + CRC(2).
                if (!await ReadExactAsync(stream, buf, 2, 5, ct).ConfigureAwait(false)) return -1;
                int bc = buf[6];
                if (!await ReadExactAsync(stream, buf, 7, bc + 2, ct).ConfigureAwait(false)) return -1;
                return 7 + bc + 2;
            case 0x17:
                // Read readAddr(2)+readQty(2)+writeAddr(2)+writeQty(2)+byteCount(1) = 9 bytes.
                if (!await ReadExactAsync(stream, buf, 2, 9, ct).ConfigureAwait(false)) return -1;
                int bc23 = buf[10];
                if (!await ReadExactAsync(stream, buf, 11, bc23 + 2, ct).ConfigureAwait(false)) return -1;
                return 11 + bc23 + 2;
            default:
                return -1;
        }
    }

    /// <summary>Listen for Modbus ASCII (':' + hex + LRC + CRLF) frames over TCP.</summary>
    public void StartAsciiOverTcp(int port)
    {
        if (AsciiOverTcpRunning) throw new InvalidOperationException("ASCII slave already running.");
        _asciiListener = new TcpListener(IPAddress.Any, port);
        _asciiListener.Start();
        _asciiCts = new CancellationTokenSource();
        var ct = _asciiCts.Token;
        _asciiLoop = Task.Run(() => AsciiAcceptLoopAsync(ct));
    }

    public void StopAsciiOverTcp()
    {
        if (!AsciiOverTcpRunning) return;
        try { _asciiCts?.Cancel(); } catch { }
        try { _asciiListener?.Stop(); } catch { }
        try { _asciiLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _asciiListener = null;
        _asciiCts?.Dispose(); _asciiCts = null; _asciiLoop = null;
    }

    private async Task AsciiAcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _asciiListener!.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch { return; }
            _ = Task.Run(() => HandleAsciiClientAsync(client, ct), ct);
        }
    }

    private async Task HandleAsciiClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            client.NoDelay = true;
            using var stream = client.GetStream();
            var buf = new byte[512];

            while (!ct.IsCancellationRequested)
            {
                int len = 0;
                while (true)
                {
                    int b;
                    try { b = stream.ReadByte(); } catch { return; }
                    if (b < 0) return;
                    if (len == 0 && b != ':') continue;          // resync on ':' frame start
                    buf[len++] = (byte)b;
                    if (len >= 2 && buf[len - 2] == '\r' && buf[len - 1] == '\n') break;
                    if (len == buf.Length) return;
                }

                if (!TryParseAsciiFrame(buf, len, out byte unitId, out byte fc, out byte[] pdu))
                    continue;
                if (!IgnoreUnitId && unitId != SlaveId && unitId != 0) continue;
                if (SkipResponses && _rng.Next(10) == 0) continue;

                // Re-pack pdu into the buffer so Dispatch (which expects buffer[off]=FC) works.
                var work = new byte[pdu.Length + 1];
                work[0] = unitId;
                Buffer.BlockCopy(pdu, 0, work, 1, pdu.Length);

                byte[] resp;
                try { resp = ReturnExceptionBusy ? BuildException(fc, 0x06) : Dispatch(work, 1, pdu.Length); }
                catch { continue; }

                if (ResponseDelayMs > 0)
                {
                    try { await Task.Delay(ResponseDelayMs, ct).ConfigureAwait(false); }
                    catch { return; }
                }
                if (unitId == 0) continue;

                var frame = WrapAscii(unitId, resp);
                try { await stream.WriteAsync(frame, ct).ConfigureAwait(false); } catch { return; }
            }
        }
        catch { /* connection lifetime is best-effort */ }
        finally { try { client.Close(); } catch { } }
    }

    private static bool TryParseAsciiFrame(byte[] buf, int len, out byte unitId, out byte fc, out byte[] pdu)
    {
        unitId = 0; fc = 0; pdu = Array.Empty<byte>();
        if (len < 7 || buf[0] != ':' || buf[len - 2] != '\r' || buf[len - 1] != '\n') return false;

        var hex = Encoding.ASCII.GetString(buf, 1, len - 3);
        if (hex.Length % 2 != 0) return false;
        var bytes = new byte[hex.Length / 2];
        try { for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16); }
        catch { return false; }

        // LRC = -sum(bytes[0..^1]) (mod 256)
        byte sum = 0;
        for (int i = 0; i < bytes.Length - 1; i++) sum += bytes[i];
        byte expected = (byte)(-(sbyte)sum);
        if (bytes[^1] != expected) return false;

        unitId = bytes[0];
        if (bytes.Length < 3) return false;
        fc = bytes[1];
        pdu = new byte[bytes.Length - 2];
        Buffer.BlockCopy(bytes, 1, pdu, 0, pdu.Length);
        return true;
    }

    private static byte[] WrapAscii(byte unitId, byte[] pdu)
    {
        byte sum = unitId;
        foreach (var b in pdu) sum += b;
        byte lrc = (byte)(-(sbyte)sum);
        var sb = new StringBuilder(":");
        sb.Append(unitId.ToString("X2"));
        foreach (var b in pdu) sb.Append(b.ToString("X2"));
        sb.Append(lrc.ToString("X2"));
        sb.Append("\r\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    /// <summary>Listen for Modbus TCP wrapped in TLS. Generates a self-signed RSA cert on first call
    /// if <paramref name="cert"/> is null — fine for local testing but not for production deployments.</summary>
    public void StartTls(int port, X509Certificate2? cert = null)
    {
        if (TlsRunning) throw new InvalidOperationException("TLS slave already running.");
        _tlsCert = cert ?? GenerateSelfSignedCert();
        _tlsListener = new TcpListener(IPAddress.Any, port);
        _tlsListener.Start();
        _tlsCts = new CancellationTokenSource();
        var ct = _tlsCts.Token;
        _tlsLoop = Task.Run(() => TlsAcceptLoopAsync(ct));
    }

    public void StopTls()
    {
        if (!TlsRunning) return;
        try { _tlsCts?.Cancel(); } catch { }
        try { _tlsListener?.Stop(); } catch { }
        try { _tlsLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _tlsListener = null;
        _tlsCert?.Dispose(); _tlsCert = null;
        _tlsCts?.Dispose(); _tlsCts = null; _tlsLoop = null;
    }

    private async Task TlsAcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _tlsListener!.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch { return; }
            _ = Task.Run(() => HandleTlsClientAsync(client, ct), ct);
        }
    }

    private async Task HandleTlsClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            client.NoDelay = true;
            await using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
            try { await ssl.AuthenticateAsServerAsync(_tlsCert!, clientCertificateRequired: false, checkCertificateRevocation: false); }
            catch { return; }

            var buffer = new byte[MaxAduLength];
            while (!ct.IsCancellationRequested)
            {
                if (!await ReadExactStreamAsync(ssl, buffer, 0, MbapHeaderLength, ct).ConfigureAwait(false)) return;
                int protocolId = (buffer[2] << 8) | buffer[3];
                int length     = (buffer[4] << 8) | buffer[5];
                byte unitId    = buffer[6];
                if (protocolId != 0 || length < 2 || length > MaxPduLength + 1) return;
                int pduLen = length - 1;
                if (!await ReadExactStreamAsync(ssl, buffer, MbapHeaderLength, pduLen, ct).ConfigureAwait(false)) return;
                if (!IgnoreUnitId && unitId != SlaveId && unitId != 0) continue;
                if (SkipResponses && _rng.Next(10) == 0) continue;

                byte[] resp;
                try { resp = ReturnExceptionBusy ? BuildException(buffer[MbapHeaderLength], 0x06)
                                                 : Dispatch(buffer, MbapHeaderLength, pduLen); }
                catch { continue; }

                if (ResponseDelayMs > 0)
                {
                    try { await Task.Delay(ResponseDelayMs, ct).ConfigureAwait(false); }
                    catch { return; }
                }

                int frameLen = resp.Length + 1;
                var frame = new byte[MbapHeaderLength + resp.Length];
                frame[0] = buffer[0]; frame[1] = buffer[1];
                frame[2] = 0; frame[3] = 0;
                frame[4] = (byte)(frameLen >> 8); frame[5] = (byte)frameLen;
                frame[6] = unitId;
                Buffer.BlockCopy(resp, 0, frame, MbapHeaderLength, resp.Length);
                try { await ssl.WriteAsync(frame, ct).ConfigureAwait(false); } catch { return; }
            }
        }
        catch { /* connection-lifetime best-effort */ }
        finally { try { client.Close(); } catch { } }
    }

    private static async Task<bool> ReadExactStreamAsync(Stream s, byte[] buf, int offset, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count)
        {
            int read = await s.ReadAsync(buf.AsMemory(offset + total, count - total), ct).ConfigureAwait(false);
            if (read == 0) return false;
            total += read;
        }
        return true;
    }

    /// <summary>Create a short-lived self-signed RSA cert for the TLS listener. Strictly for testing —
    /// production users should pass their own <see cref="X509Certificate2"/> to <see cref="StartTls"/>.</summary>
    private static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=OpenSlave", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        // Round-trip through PFX so the cert carries an exportable private key (required by SslStream).
        return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
    }

    /// <summary>Open a serial port and service Modbus ASCII frames (7-bit, ':' + hex + LRC + CRLF).</summary>
    public void StartAsciiOverSerial(string portName, int baudRate = 9600, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
    {
        if (AsciiSerialRunning) throw new InvalidOperationException("ASCII-over-serial slave already running.");
        _asciiSerial = new SerialPort(portName, baudRate, parity, 7, stopBits)
        {
            ReadTimeout = 5000,
            WriteTimeout = 1000,
            NewLine = "\r\n",
        };
        _asciiSerial.Open();
        _asciiSerialCts = new CancellationTokenSource();
        var ct = _asciiSerialCts.Token;
        _asciiSerialLoop = Task.Run(() => AsciiSerialReadLoop(ct));
    }

    public void StopAsciiOverSerial()
    {
        if (!AsciiSerialRunning) return;
        try { _asciiSerialCts?.Cancel(); } catch { }
        try { _asciiSerial?.Close(); } catch { }
        try { _asciiSerialLoop?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _asciiSerial = null;
        _asciiSerialCts?.Dispose(); _asciiSerialCts = null; _asciiSerialLoop = null;
    }

    private void AsciiSerialReadLoop(CancellationToken ct)
    {
        var port = _asciiSerial!;
        while (!ct.IsCancellationRequested && port.IsOpen)
        {
            string body;
            try { body = port.ReadLine(); }
            catch (TimeoutException) { continue; }
            catch (OperationCanceledException) { return; }
            catch { return; }

            var raw = Encoding.ASCII.GetBytes(body + "\r\n");
            if (!TryParseAsciiFrame(raw, raw.Length, out byte unitId, out byte fc, out byte[] pdu))
                continue;
            if (!IgnoreUnitId && unitId != SlaveId && unitId != 0) continue;
            if (SkipResponses && _rng.Next(10) == 0) continue;

            var work = new byte[pdu.Length + 1];
            work[0] = unitId;
            Buffer.BlockCopy(pdu, 0, work, 1, pdu.Length);

            byte[] resp;
            try { resp = ReturnExceptionBusy ? BuildException(fc, 0x06) : Dispatch(work, 1, pdu.Length); }
            catch { continue; }

            if (ResponseDelayMs > 0) Thread.Sleep(ResponseDelayMs);
            if (unitId == 0) continue;

            var frame = WrapAscii(unitId, resp);
            try { port.Write(frame, 0, frame.Length); } catch { return; }
        }
    }

    /// <summary>Listen for Modbus RTU framing inside UDP datagrams.</summary>
    public void StartRtuOverUdp(int port)
    {
        if (RtuOverUdpRunning) throw new InvalidOperationException("RTU-over-UDP slave already running.");
        _udpRtu = new UdpClient(port);
        _udpRtuCts = new CancellationTokenSource();
        var ct = _udpRtuCts.Token;
        _udpRtuLoop = Task.Run(() => UdpRtuLoopAsync(ct));
    }

    public void StopRtuOverUdp()
    {
        if (!RtuOverUdpRunning) return;
        try { _udpRtuCts?.Cancel(); } catch { }
        try { _udpRtu?.Close(); } catch { }
        try { _udpRtuLoop?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _udpRtu = null;
        _udpRtuCts?.Dispose(); _udpRtuCts = null; _udpRtuLoop = null;
    }

    private async Task UdpRtuLoopAsync(CancellationToken ct)
    {
        var udp = _udpRtu!;
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult datagram;
            try { datagram = await udp.ReceiveAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch { continue; }

            var buf = datagram.Buffer;
            if (buf.Length < 4 || !ModbusCrc.Verify(buf, 0, buf.Length)) continue;
            byte unitId = buf[0];
            if (!IgnoreUnitId && unitId != SlaveId && unitId != 0) continue;
            if (SkipResponses && _rng.Next(10) == 0) continue;

            int pduLen = buf.Length - 3;
            byte[] resp;
            try { resp = ReturnExceptionBusy ? BuildException(buf[1], 0x06) : Dispatch(buf, 1, pduLen); }
            catch { continue; }

            if (ResponseDelayMs > 0)
            {
                try { await Task.Delay(ResponseDelayMs, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
            if (unitId == 0) continue;

            var frame = ModbusCrc.WrapRtu(unitId, resp);
            try { await udp.SendAsync(frame, frame.Length, datagram.RemoteEndPoint).ConfigureAwait(false); } catch { }
        }
    }

    /// <summary>Listen for Modbus ASCII framing inside UDP datagrams.</summary>
    public void StartAsciiOverUdp(int port)
    {
        if (AsciiOverUdpRunning) throw new InvalidOperationException("ASCII-over-UDP slave already running.");
        _udpAscii = new UdpClient(port);
        _udpAsciiCts = new CancellationTokenSource();
        var ct = _udpAsciiCts.Token;
        _udpAsciiLoop = Task.Run(() => UdpAsciiLoopAsync(ct));
    }

    public void StopAsciiOverUdp()
    {
        if (!AsciiOverUdpRunning) return;
        try { _udpAsciiCts?.Cancel(); } catch { }
        try { _udpAscii?.Close(); } catch { }
        try { _udpAsciiLoop?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _udpAscii = null;
        _udpAsciiCts?.Dispose(); _udpAsciiCts = null; _udpAsciiLoop = null;
    }

    private async Task UdpAsciiLoopAsync(CancellationToken ct)
    {
        var udp = _udpAscii!;
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult datagram;
            try { datagram = await udp.ReceiveAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch { continue; }

            if (!TryParseAsciiFrame(datagram.Buffer, datagram.Buffer.Length, out byte unitId, out byte fc, out byte[] pdu)) continue;
            if (!IgnoreUnitId && unitId != SlaveId && unitId != 0) continue;
            if (SkipResponses && _rng.Next(10) == 0) continue;

            var work = new byte[pdu.Length + 1];
            work[0] = unitId;
            Buffer.BlockCopy(pdu, 0, work, 1, pdu.Length);

            byte[] resp;
            try { resp = ReturnExceptionBusy ? BuildException(fc, 0x06) : Dispatch(work, 1, pdu.Length); }
            catch { continue; }

            if (ResponseDelayMs > 0)
            {
                try { await Task.Delay(ResponseDelayMs, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
            if (unitId == 0) continue;

            var frame = WrapAscii(unitId, resp);
            try { await udp.SendAsync(frame, frame.Length, datagram.RemoteEndPoint).ConfigureAwait(false); } catch { }
        }
    }

    public void Dispose() { Stop(); StopSerial(); StopUdp(); StopRtuOverTcp(); StopAsciiOverTcp(); StopTls();
        StopAsciiOverSerial(); StopRtuOverUdp(); StopAsciiOverUdp(); }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener!.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { return; }

            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        ConnectedClientsChanged?.Invoke(Interlocked.Increment(ref _connectedClients));
        try
        {
            client.NoDelay = true;
            using var stream = client.GetStream();
            var buffer = new byte[MaxAduLength];

            while (!ct.IsCancellationRequested)
            {
                if (!await ReadExactAsync(stream, buffer, 0, MbapHeaderLength, ct).ConfigureAwait(false))
                    return;

                int transactionId = (buffer[0] << 8) | buffer[1];
                int protocolId    = (buffer[2] << 8) | buffer[3];
                int length        = (buffer[4] << 8) | buffer[5];
                byte unitId       = buffer[6];

                if (protocolId != 0 || length < 2 || length > MaxPduLength + 1)
                    return; // malformed; drop the connection.

                int pduLen = length - 1;
                if (!await ReadExactAsync(stream, buffer, MbapHeaderLength, pduLen, ct).ConfigureAwait(false))
                    return;

                if (!IgnoreUnitId && unitId != SlaveId && unitId != 0)
                    continue; // not for us — ignore silently

                if (SkipResponses && _rng.Next(10) == 0)
                    continue; // 1-in-10 silent drop — flaky-link simulation

                byte[] response = ReturnExceptionBusy
                    ? BuildException(buffer[MbapHeaderLength], 0x06)
                    : Dispatch(buffer, MbapHeaderLength, pduLen);

                if (ResponseDelayMs > 0)
                {
                    try { await Task.Delay(ResponseDelayMs, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }

                int frameLen = response.Length + 1;
                var frame = new byte[MbapHeaderLength + response.Length];
                frame[0] = (byte)(transactionId >> 8);
                frame[1] = (byte)transactionId;
                frame[2] = 0;
                frame[3] = 0;
                frame[4] = (byte)(frameLen >> 8);
                frame[5] = (byte)frameLen;
                frame[6] = unitId;
                Buffer.BlockCopy(response, 0, frame, MbapHeaderLength, response.Length);

                try { await stream.WriteAsync(frame, ct).ConfigureAwait(false); }
                catch { return; }
            }
        }
        catch { /* swallow; connection lifetime is best-effort */ }
        finally
        {
            try { client.Close(); } catch { }
            ConnectedClientsChanged?.Invoke(Interlocked.Decrement(ref _connectedClients));
        }
    }

    private byte[] Dispatch(byte[] buffer, int pduOffset, int pduLen)
    {
        byte fc = buffer[pduOffset];
        try
        {
            return fc switch
            {
                0x01 => HandleReadBits(buffer, pduOffset, pduLen, Coils, fc),
                0x02 => HandleReadBits(buffer, pduOffset, pduLen, DiscreteInputs, fc),
                0x03 => HandleReadRegisters(buffer, pduOffset, pduLen, HoldingRegisters, fc),
                0x04 => HandleReadRegisters(buffer, pduOffset, pduLen, InputRegisters, fc),
                0x05 => HandleWriteSingleCoil(buffer, pduOffset, pduLen),
                0x06 => HandleWriteSingleRegister(buffer, pduOffset, pduLen),
                0x0F => HandleWriteMultipleCoils(buffer, pduOffset, pduLen),
                0x10 => HandleWriteMultipleRegisters(buffer, pduOffset, pduLen),
                0x16 => HandleMaskWriteRegister(buffer, pduOffset, pduLen),
                0x17 => HandleReadWriteMultipleRegisters(buffer, pduOffset, pduLen),
                0x2B => HandleReadDeviceIdentification(buffer, pduOffset, pduLen),
                0x08 => HandleDiagnostic(buffer, pduOffset, pduLen),
                0x0B => HandleGetCommEventCounter(buffer, pduOffset, pduLen),
                0x11 => HandleReportServerId(buffer, pduOffset, pduLen),
                _    => BuildException(fc, 0x01), // illegal function
            };
        }
        catch (ProtocolException px)
        {
            return BuildException(fc, px.ExceptionCode);
        }
    }

    // ─────── Read function codes ─────────────────────────────────────

    private byte[] HandleReadBits(byte[] buf, int off, int len, bool[] table, byte fc)
    {
        Require(len == 5, fc, 0x03);
        int address = (buf[off + 1] << 8) | buf[off + 2];
        int quantity = (buf[off + 3] << 8) | buf[off + 4];
        if (quantity < 1 || quantity > 2000) throw new ProtocolException(0x03);
        if (address < 0 || address + quantity > TableSize) throw new ProtocolException(0x02);

        int byteCount = (quantity + 7) / 8;
        var resp = new byte[2 + byteCount];
        resp[0] = fc;
        resp[1] = (byte)byteCount;
        for (int i = 0; i < quantity; i++)
        {
            if (table[address + i]) resp[2 + (i / 8)] |= (byte)(1 << (i % 8));
        }
        RequestHandled?.Invoke(new RequestEvent(fc, address, quantity, ""));
        return resp;
    }

    private byte[] HandleReadRegisters(byte[] buf, int off, int len, ushort[] table, byte fc)
    {
        Require(len == 5, fc, 0x03);
        int address = (buf[off + 1] << 8) | buf[off + 2];
        int quantity = (buf[off + 3] << 8) | buf[off + 4];
        if (quantity < 1 || quantity > 125) throw new ProtocolException(0x03);
        if (address < 0 || address + quantity > TableSize) throw new ProtocolException(0x02);

        var resp = new byte[2 + quantity * 2];
        resp[0] = fc;
        resp[1] = (byte)(quantity * 2);
        for (int i = 0; i < quantity; i++)
        {
            ushort w = table[address + i];
            resp[2 + i * 2]     = (byte)(w >> 8);
            resp[2 + i * 2 + 1] = (byte)w;
        }
        RequestHandled?.Invoke(new RequestEvent(fc, address, quantity, ""));
        return resp;
    }

    // ─────── Write function codes ────────────────────────────────────

    private byte[] HandleWriteSingleCoil(byte[] buf, int off, int len)
    {
        Require(len == 5, 0x05, 0x03);
        int address = (buf[off + 1] << 8) | buf[off + 2];
        int value = (buf[off + 3] << 8) | buf[off + 4];
        if (value != 0x0000 && value != 0xFF00) throw new ProtocolException(0x03);
        if (address < 0 || address >= TableSize) throw new ProtocolException(0x02);

        Coils[address] = (value == 0xFF00);
        CoilsChanged?.Invoke(address, 1);
        RequestHandled?.Invoke(new RequestEvent(0x05, address, 1, value == 0xFF00 ? "= 1" : "= 0"));
        return new[] { (byte)0x05, buf[off + 1], buf[off + 2], buf[off + 3], buf[off + 4] };
    }

    private byte[] HandleWriteSingleRegister(byte[] buf, int off, int len)
    {
        Require(len == 5, 0x06, 0x03);
        int address = (buf[off + 1] << 8) | buf[off + 2];
        ushort value = (ushort)((buf[off + 3] << 8) | buf[off + 4]);
        if (address < 0 || address >= TableSize) throw new ProtocolException(0x02);

        HoldingRegisters[address] = value;
        HoldingRegistersChanged?.Invoke(address, 1);
        RequestHandled?.Invoke(new RequestEvent(0x06, address, 1, $"= {value}"));
        return new[] { (byte)0x06, buf[off + 1], buf[off + 2], buf[off + 3], buf[off + 4] };
    }

    private byte[] HandleWriteMultipleCoils(byte[] buf, int off, int len)
    {
        Require(len >= 6, 0x0F, 0x03);
        int address = (buf[off + 1] << 8) | buf[off + 2];
        int quantity = (buf[off + 3] << 8) | buf[off + 4];
        int byteCount = buf[off + 5];
        if (quantity < 1 || quantity > 1968 || byteCount != (quantity + 7) / 8 || len != 6 + byteCount)
            throw new ProtocolException(0x03);
        if (address < 0 || address + quantity > TableSize) throw new ProtocolException(0x02);

        for (int i = 0; i < quantity; i++)
        {
            int b = buf[off + 6 + (i / 8)];
            Coils[address + i] = (b & (1 << (i % 8))) != 0;
        }
        CoilsChanged?.Invoke(address, quantity);
        RequestHandled?.Invoke(new RequestEvent(0x0F, address, quantity, ""));
        return new[] { (byte)0x0F, buf[off + 1], buf[off + 2], buf[off + 3], buf[off + 4] };
    }

    private byte[] HandleWriteMultipleRegisters(byte[] buf, int off, int len)
    {
        Require(len >= 6, 0x10, 0x03);
        int address = (buf[off + 1] << 8) | buf[off + 2];
        int quantity = (buf[off + 3] << 8) | buf[off + 4];
        int byteCount = buf[off + 5];
        if (quantity < 1 || quantity > 123 || byteCount != quantity * 2 || len != 6 + byteCount)
            throw new ProtocolException(0x03);
        if (address < 0 || address + quantity > TableSize) throw new ProtocolException(0x02);

        for (int i = 0; i < quantity; i++)
        {
            HoldingRegisters[address + i] = (ushort)((buf[off + 6 + i * 2] << 8) | buf[off + 7 + i * 2]);
        }
        HoldingRegistersChanged?.Invoke(address, quantity);
        RequestHandled?.Invoke(new RequestEvent(0x10, address, quantity, ""));
        return new[] { (byte)0x10, buf[off + 1], buf[off + 2], buf[off + 3], buf[off + 4] };
    }

    private byte[] HandleMaskWriteRegister(byte[] buf, int off, int len)
    {
        Require(len == 7, 0x16, 0x03);
        int address = (buf[off + 1] << 8) | buf[off + 2];
        ushort andMask = (ushort)((buf[off + 3] << 8) | buf[off + 4]);
        ushort orMask  = (ushort)((buf[off + 5] << 8) | buf[off + 6]);
        if (address < 0 || address >= TableSize) throw new ProtocolException(0x02);

        // result = (current AND andMask) OR (orMask AND (NOT andMask))
        lock (_gate)
        {
            ushort current = HoldingRegisters[address];
            ushort result = (ushort)((current & andMask) | (orMask & ~andMask));
            HoldingRegisters[address] = result;
        }
        HoldingRegistersChanged?.Invoke(address, 1);
        RequestHandled?.Invoke(new RequestEvent(0x16, address, 1, $"AND={andMask:X4} OR={orMask:X4}"));

        var echo = new byte[7];
        echo[0] = 0x16;
        Buffer.BlockCopy(buf, off + 1, echo, 1, 6);
        return echo;
    }

    private byte[] HandleReadWriteMultipleRegisters(byte[] buf, int off, int len)
    {
        Require(len >= 10, 0x17, 0x03);
        int readAddr = (buf[off + 1] << 8) | buf[off + 2];
        int readQty  = (buf[off + 3] << 8) | buf[off + 4];
        int writeAddr = (buf[off + 5] << 8) | buf[off + 6];
        int writeQty  = (buf[off + 7] << 8) | buf[off + 8];
        int byteCount = buf[off + 9];

        if (readQty  < 1 || readQty  > 125)  throw new ProtocolException(0x03);
        if (writeQty < 1 || writeQty > 121)  throw new ProtocolException(0x03);
        if (byteCount != writeQty * 2 || len != 10 + byteCount) throw new ProtocolException(0x03);
        if (readAddr  < 0 || readAddr  + readQty  > TableSize) throw new ProtocolException(0x02);
        if (writeAddr < 0 || writeAddr + writeQty > TableSize) throw new ProtocolException(0x02);

        lock (_gate)
        {
            for (int i = 0; i < writeQty; i++)
            {
                HoldingRegisters[writeAddr + i] = (ushort)((buf[off + 10 + i * 2] << 8) | buf[off + 11 + i * 2]);
            }
        }
        HoldingRegistersChanged?.Invoke(writeAddr, writeQty);

        var resp = new byte[2 + readQty * 2];
        resp[0] = 0x17;
        resp[1] = (byte)(readQty * 2);
        for (int i = 0; i < readQty; i++)
        {
            ushort w = HoldingRegisters[readAddr + i];
            resp[2 + i * 2]     = (byte)(w >> 8);
            resp[2 + i * 2 + 1] = (byte)w;
        }
        RequestHandled?.Invoke(new RequestEvent(0x17, readAddr, readQty, $"+ wrote {writeQty} @ {writeAddr}"));
        return resp;
    }

    private byte[] HandleDiagnostic(byte[] buf, int off, int len)
    {
        // PDU: 0x08 SubFn(2) Data(2)  — minimum length 5
        Require(len == 5, 0x08, 0x03);
        ushort sub = (ushort)((buf[off + 1] << 8) | buf[off + 2]);
        ushort data = (ushort)((buf[off + 3] << 8) | buf[off + 4]);

        ushort respData = sub switch
        {
            0x0000 => data,                                                              // Return Query Data
            0x0001 => (ushort)(ResetCounters(restart: true) ? 0x0000 : 0x0000),          // Restart Comm Option (no listen-only side-effect)
            0x0002 => _diagRegister,                                                     // Return Diagnostic Register
            0x0004 => 0x0000,                                                            // Force Listen Only Mode — accepted (advisory)
            0x000A => ResetCountersReturn(),                                             // Clear Counters and Diag Register
            0x000B => (ushort)Volatile.Read(ref _commEventCounter),                      // Return Bus Message Count
            0x000C => (ushort)Volatile.Read(ref _commErrorCounter),                      // Return Bus Comm Error Count
            0x000D => (ushort)Volatile.Read(ref _exceptionCounter),                      // Return Bus Exception Error Count
            0x000E => (ushort)Volatile.Read(ref _commEventCounter),                      // Return Slave Message Count
            0x000F => (ushort)Volatile.Read(ref _slaveNoResponseCounter),                // Return Slave No Response Count
            0x0010 => 0x0000,                                                            // Return Slave NAK Count — n/a on TCP/UDP
            0x0011 => (ushort)Volatile.Read(ref _slaveBusyCounter),                      // Return Slave Busy Count
            0x0012 => 0x0000,                                                            // Return Bus Character Overrun Count — n/a on TCP/UDP
            _      => throw new ProtocolException(0x01),
        };

        Interlocked.Increment(ref _commEventCounter);
        var resp = new byte[]
        {
            0x08, (byte)(sub >> 8), (byte)sub, (byte)(respData >> 8), (byte)respData,
        };
        RequestHandled?.Invoke(new RequestEvent(0x08, 0, 0, $"sub={sub:X4} → {respData:X4}"));
        return resp;
    }

    private bool ResetCounters(bool restart = false)
    {
        Interlocked.Exchange(ref _commErrorCounter, 0);
        Interlocked.Exchange(ref _exceptionCounter, 0);
        Interlocked.Exchange(ref _slaveNoResponseCounter, 0);
        Interlocked.Exchange(ref _slaveBusyCounter, 0);
        _diagRegister = 0;
        if (restart) Interlocked.Exchange(ref _commEventCounter, 0);
        return true;
    }

    private ushort ResetCountersReturn()
    {
        ResetCounters();
        return 0x0000;
    }

    private byte[] HandleGetCommEventCounter(byte[] buf, int off, int len)
    {
        // PDU: 0x0B  (request has no payload)
        Require(len == 1, 0x0B, 0x03);
        var counter = (ushort)Volatile.Read(ref _commEventCounter);
        // Status word 0xFFFF means "previous command still active"; we always return idle (0x0000).
        var resp = new byte[]
        {
            0x0B,
            0x00, 0x00,
            (byte)(counter >> 8), (byte)counter,
        };
        Interlocked.Increment(ref _commEventCounter);
        RequestHandled?.Invoke(new RequestEvent(0x0B, 0, 0, $"counter={counter}"));
        return resp;
    }

    private byte[] HandleReportServerId(byte[] buf, int off, int len)
    {
        // PDU: 0x11  (request has no payload)
        Require(len == 1, 0x11, 0x03);
        var id = System.Text.Encoding.ASCII.GetBytes(DeviceIdentification.ProductName ?? "OpenSlave");
        // ByteCount + ServerId(N) + RunIndicator (0xFF = ON, 0x00 = OFF). We're always ON when serving.
        var resp = new byte[2 + id.Length + 1];
        resp[0] = 0x11;
        resp[1] = (byte)(id.Length + 1);
        Buffer.BlockCopy(id, 0, resp, 2, id.Length);
        resp[^1] = 0xFF;
        Interlocked.Increment(ref _commEventCounter);
        RequestHandled?.Invoke(new RequestEvent(0x11, 0, 0, $"server-id"));
        return resp;
    }

    private byte[] HandleReadDeviceIdentification(byte[] buf, int off, int len)
    {
        // PDU: 0x2B 0x0E ReadDevIdCode ObjectId
        Require(len == 4, 0x2B, 0x03);
        byte meiType = buf[off + 1];
        byte code    = buf[off + 2];
        byte objectId = buf[off + 3];
        if (meiType != 0x0E) throw new ProtocolException(0x01);   // illegal function for unknown MEI
        if (code < 0x01 || code > 0x04) throw new ProtocolException(0x03);

        var (maxId, conformity) = code switch
        {
            0x01 => ((byte)0x02, (byte)0x81),  // basic + individual access
            0x02 => ((byte)0x06, (byte)0x82),  // regular + individual access
            0x03 => ((byte)0x06, (byte)0x83),  // extended (we have no vendor objects, return same set)
            0x04 => ((byte)0xFF, (byte)0x83),  // individual: caller picks the id
            _    => ((byte)0x00, (byte)0x00),
        };

        // Stream access (0x01..0x03): return every present object with id in [objectId, maxId].
        // Individual access (0x04): return just objectId, or exception 0x02 if absent/empty.
        var picks = new System.Collections.Generic.List<(byte Id, byte[] Bytes)>();
        if (code == 0x04)
        {
            var value = DeviceIdentification.GetUtf8(objectId);
            if (value.Length == 0) throw new ProtocolException(0x02);
            picks.Add((objectId, value));
        }
        else
        {
            for (byte id = objectId; id <= maxId; id++)
            {
                var value = DeviceIdentification.GetUtf8(id);
                if (value.Length > 0) picks.Add((id, value));
            }
        }

        // PDU header: FC, MEI, Code, Conformity, MoreFollows, NextObjectId, NumberOfObjects = 7 bytes,
        // plus 2 bytes (id + length) per object plus the value bytes.
        const int PduBudget = 253;
        var body = new System.Collections.Generic.List<byte>(64);
        byte moreFollows = 0x00;
        byte nextId = 0x00;
        int written = 0;
        foreach (var (id, value) in picks)
        {
            int objBytes = 2 + value.Length;
            if (7 + body.Count + objBytes > PduBudget)
            {
                moreFollows = 0xFF;
                nextId = id;
                break;
            }
            body.Add(id);
            body.Add((byte)value.Length);
            body.AddRange(value);
            written++;
        }

        var resp = new byte[7 + body.Count];
        resp[0] = 0x2B;
        resp[1] = 0x0E;
        resp[2] = code;
        resp[3] = conformity;
        resp[4] = moreFollows;
        resp[5] = nextId;
        resp[6] = (byte)written;
        for (int i = 0; i < body.Count; i++) resp[7 + i] = body[i];
        RequestHandled?.Invoke(new RequestEvent(0x2B, objectId, written, $"code={code:X2}"));
        return resp;
    }

    // ─────── helpers ─────────────────────────────────────────────────

    private static void Require(bool condition, byte fc, byte exception)
    {
        if (!condition) throw new ProtocolException(exception);
    }

    private static byte[] BuildException(byte fc, byte code) =>
        new[] { (byte)(fc | 0x80), code };

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buf, int offset, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buf.AsMemory(offset + total, count - total), ct).ConfigureAwait(false);
            if (read == 0) return false;
            total += read;
        }
        return true;
    }

    private sealed class ProtocolException : Exception
    {
        public byte ExceptionCode { get; }
        public ProtocolException(byte code) { ExceptionCode = code; }
    }

    public readonly record struct RequestEvent(byte FunctionCode, int Address, int Quantity, string Detail);
}
