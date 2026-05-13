using System;
using System.Net;
using System.Net.Sockets;
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
    private int _commEventCounter;   // increments per non-broadcast request (FC 11 surface)

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

    public void Dispose() => Stop();

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
        // Sub-function 0x0000 (Return Query Data) — echo whatever bytes the client sent.
        if (sub != 0x0000) throw new ProtocolException(0x01);
        var resp = new byte[5];
        Buffer.BlockCopy(buf, off, resp, 0, 5);
        Interlocked.Increment(ref _commEventCounter);
        RequestHandled?.Invoke(new RequestEvent(0x08, 0, 0, $"sub={sub:X4} echo"));
        return resp;
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
