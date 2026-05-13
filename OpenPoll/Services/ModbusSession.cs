using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using NModbus;
using NModbus.IO;
using OpenPoll.Models;

namespace OpenPoll.Services;

/// <summary>
/// Modbus master session, backed by NModbus 3.x.
///
/// Public API surface kept stable so callers (PollDocument, HttpApiHost, Cli, Views) need no changes.
/// All wire calls go through Read/Write helpers that lock, retry, and translate exceptions to
/// <see cref="ModbusResult"/>. Modbus protocol exceptions (illegal address, slave busy, etc.) are
/// surfaced via <see cref="SlaveException"/> from NModbus and reported with their numeric code.
/// </summary>
public sealed class ModbusSession : IDisposable
{
    private readonly object _lock = new();
    private readonly IModbusFactory _factory = new ModbusFactory();

    private TcpClient? _tcp;
    private SerialPort? _serial;
    private IModbusMaster? _master;
    private byte _unitId = 1;
    private PollDefinition? _appliedSettings;

    public bool Connected
    {
        get
        {
            lock (_lock)
            {
                return _master is not null && (_tcp?.Connected ?? _serial?.IsOpen ?? false);
            }
        }
    }

    public ModbusResult Connect(PollDefinition settings)
    {
        lock (_lock)
        {
            try
            {
                if (_master is not null && _appliedSettings is not null && SameTransport(_appliedSettings, settings))
                {
                    ApplyTunables(settings);
                    return ModbusResult.Ok();
                }

                Teardown();

                if (settings.ConnectionMode == ConnectionMode.Tcp)
                {
                    _tcp = new TcpClient();
                    var connectAr = _tcp.BeginConnect(settings.IpAddress, settings.ServerPort, null, null);
                    if (!connectAr.AsyncWaitHandle.WaitOne(Math.Max(50, settings.ConnectionTimeoutMs)))
                    {
                        Teardown();
                        return ModbusResult.Fail("Connect timeout");
                    }
                    _tcp.EndConnect(connectAr);
                    _master = _factory.CreateMaster(_tcp);
                }
                else
                {
                    _serial = new SerialPort(settings.SerialPortName)
                    {
                        BaudRate = settings.BaudRate,
                        Parity = settings.Parity,
                        StopBits = settings.StopBits,
                        DataBits = 8,
                        ReadTimeout = Math.Max(50, settings.ResponseTimeoutMs),
                        WriteTimeout = Math.Max(50, settings.ResponseTimeoutMs),
                    };
                    _serial.Open();
                    _master = _factory.CreateRtuMaster(new SerialStreamResource(_serial));
                }

                ApplyTunables(settings);
                _appliedSettings = settings.Clone();
                return ModbusResult.Ok();
            }
            catch (Exception ex)
            {
                Teardown();
                return ModbusResult.Fail(Describe(ex));
            }
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            Teardown();
            _appliedSettings = null;
        }
    }

    private void Teardown()
    {
        try { _master?.Dispose(); } catch { }
        try { _tcp?.Close(); } catch { }
        try { _serial?.Close(); } catch { }
        _master = null;
        _tcp = null;
        _serial = null;
    }

    public ModbusResult<bool[]> ReadCoils(int address, int quantity)
    {
        if (RangeError(address, quantity, 2000, out var err)) return ModbusResult<bool[]>.Fail(err);
        return Read("01 ReadCoils", address, quantity,
            m => m.ReadCoils(_unitId, (ushort)address, (ushort)quantity), SummarizeBools);
    }

    public ModbusResult<bool[]> ReadDiscreteInputs(int address, int quantity)
    {
        if (RangeError(address, quantity, 2000, out var err)) return ModbusResult<bool[]>.Fail(err);
        return Read("02 ReadDiscreteInputs", address, quantity,
            m => m.ReadInputs(_unitId, (ushort)address, (ushort)quantity), SummarizeBools);
    }

    public ModbusResult<int[]> ReadHoldingRegisters(int address, int quantity)
    {
        if (RangeError(address, quantity, 125, out var err)) return ModbusResult<int[]>.Fail(err);
        return Read("03 ReadHoldingRegisters", address, quantity,
            m => m.ReadHoldingRegisters(_unitId, (ushort)address, (ushort)quantity).Select(u => (int)u).ToArray(),
            SummarizeInts);
    }

    public ModbusResult<int[]> ReadInputRegisters(int address, int quantity)
    {
        if (RangeError(address, quantity, 125, out var err)) return ModbusResult<int[]>.Fail(err);
        return Read("04 ReadInputRegisters", address, quantity,
            m => m.ReadInputRegisters(_unitId, (ushort)address, (ushort)quantity).Select(u => (int)u).ToArray(),
            SummarizeInts);
    }

    public ModbusResult WriteSingleCoil(int address, bool value)
    {
        if (RangeError(address, 1, 1, out var err)) return ModbusResult.Fail(err);
        return Write("05 WriteSingleCoil", address, 1,
            m => m.WriteSingleCoil(_unitId, (ushort)address, value), $"= {(value ? "1" : "0")}");
    }

    public ModbusResult WriteSingleRegister(int address, int value)
    {
        if (RangeError(address, 1, 1, out var err)) return ModbusResult.Fail(err);
        return Write("06 WriteSingleRegister", address, 1,
            m => m.WriteSingleRegister(_unitId, (ushort)address, (ushort)value), $"= {value}");
    }

    public ModbusResult WriteMultipleCoils(int address, bool[] values)
    {
        if (RangeError(address, values.Length, 1968, out var err)) return ModbusResult.Fail(err);
        return Write("0F WriteMultipleCoils", address, values.Length,
            m => m.WriteMultipleCoils(_unitId, (ushort)address, values), SummarizeBools(values));
    }

    public ModbusResult WriteMultipleRegisters(int address, int[] values)
    {
        if (RangeError(address, values.Length, 123, out var err)) return ModbusResult.Fail(err);
        return Write("10 WriteMultipleRegisters", address, values.Length,
            m => m.WriteMultipleRegisters(_unitId, (ushort)address, values.Select(i => (ushort)i).ToArray()),
            SummarizeInts(values));
    }

    /// <summary>Validates address + quantity for the 16-bit Modbus address space and per-FC quantity limits.</summary>
    private static bool RangeError(int address, int quantity, int maxQuantity, out string error)
    {
        if (address < 0 || address > 0xFFFF) { error = $"Illegal data address: {address} out of range 0..65535"; return true; }
        if (quantity < 1 || quantity > maxQuantity) { error = $"Illegal data value: quantity {quantity} out of range 1..{maxQuantity}"; return true; }
        if (address + quantity > 0x10000) { error = $"Illegal data address: {address}+{quantity} crosses 65536 boundary"; return true; }
        error = "";
        return false;
    }

    /// <summary>
    /// FC 22 Mask Write Register. NModbus 3.x doesn't surface this in the master interface,
    /// so we frame the PDU ourselves on TCP for atomic on-the-wire delivery, and fall back to
    /// Read-Holding + Write-Single (R-M-W) on serial RTU. Atomicity matters when a second
    /// master could write between the read and the write — TCP users now have that protection.
    /// </summary>
    public ModbusResult MaskWriteRegister(int address, ushort andMask, ushort orMask)
    {
        if (RangeError(address, 1, 1, out var err)) return ModbusResult.Fail(err);

        lock (_lock)
        {
            if (_master is null || !Connected)
            {
                LogError("16 MaskWriteRegister", address, 1, "Not connected");
                return ModbusResult.Fail("Not connected");
            }

            // TCP fast-path: send a real FC 22 PDU. Serial RTU still uses the R-M-W emulation
            // below because raw bytes would need their own CRC + retry handling.
            if (_appliedSettings?.ConnectionMode == ConnectionMode.Tcp && _tcp is not null)
            {
                int attempts = Math.Max(1, 1 + RetriesSetting);
                string lastError = "Unknown";
                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    LogTx("16 MaskWriteRegister", address, 1, $"AND={andMask:X4} OR={orMask:X4}{(attempt > 1 ? $" (retry {attempt - 1})" : "")}");
                    try
                    {
                        var pdu = SendRawTcpPdu(new byte[]
                        {
                            0x16,
                            (byte)(address >> 8), (byte)address,
                            (byte)(andMask >> 8), (byte)andMask,
                            (byte)(orMask  >> 8), (byte)orMask,
                        });
                        if (pdu.Length >= 2 && (pdu[0] & 0x80) != 0)
                        {
                            byte ex = pdu[1];
                            lastError = $"Modbus exception {ex:X2} ({SlaveExceptionName(ex)})";
                            LogError("16 MaskWriteRegister", address, 1, lastError);
                            if (attempt == attempts) break;
                            continue;
                        }
                        LogRx("16 MaskWriteRegister", address, 1, "ack (atomic)");
                        return ModbusResult.Ok();
                    }
                    catch (Exception ex)
                    {
                        lastError = Describe(ex);
                        LogError("16 MaskWriteRegister", address, 1, lastError);
                        if (attempt == attempts) break;
                    }
                }
                return ModbusResult.Fail(lastError);
            }
        }

        // Serial fallback (or any other transport) — non-atomic R-M-W under the same lock.
        return Write("16 MaskWriteRegister", address, 1, m =>
        {
            var current = m.ReadHoldingRegisters(_unitId, (ushort)address, 1)[0];
            var result = (ushort)((current & andMask) | (orMask & ~andMask));
            m.WriteSingleRegister(_unitId, (ushort)address, result);
        }, $"AND={andMask:X4} OR={orMask:X4} (R-M-W)");
    }

    /// <summary>FC 23 Read/Write Multiple Registers — atomic write-then-read.</summary>
    public ModbusResult<int[]> ReadWriteMultipleRegisters(int writeAddress, int[] writeValues, int readAddress, int readQuantity) =>
        Read("17 ReadWriteMultipleRegisters", readAddress, readQuantity,
            m => m.ReadWriteMultipleRegisters(_unitId,
                (ushort)readAddress, (ushort)readQuantity,
                (ushort)writeAddress, writeValues.Select(i => (ushort)i).ToArray())
                .Select(u => (int)u).ToArray(),
            v => $"+ wrote {writeValues.Length} @ {writeAddress} → {SummarizeInts(v)}");

    /// <summary>
    /// FC 43 / MEI 0x0E Read Device Identification. NModbus 3.x doesn't surface this as a typed
    /// call, so we frame the PDU ourselves and send it through the active transport's stream.
    /// TCP only for now; the serial RTU framing path is Wave 3.
    /// </summary>
    public ModbusResult<DeviceIdentification> ReadDeviceIdentification(
        ReadDeviceIdCode code = ReadDeviceIdCode.Basic, byte objectId = 0)
    {
        lock (_lock)
        {
            if (_master is null || !Connected)
            {
                LogError("2B ReadDeviceIdentification", 0, 0, "Not connected");
                return ModbusResult<DeviceIdentification>.Fail("Not connected");
            }
            if (_appliedSettings is null || _appliedSettings.ConnectionMode != ConnectionMode.Tcp || _tcp is null)
            {
                return ModbusResult<DeviceIdentification>.Fail("FC 43 over serial RTU not yet supported");
            }

            int attempts = Math.Max(1, 1 + RetriesSetting);
            string lastError = "Unknown";
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                LogTx("2B ReadDeviceIdentification", objectId, 0, $"code={(byte)code:X2}{(attempt > 1 ? $" (retry {attempt - 1})" : "")}");
                try
                {
                    var pdu = SendRawTcpPdu(new byte[] { 0x2B, 0x0E, (byte)code, objectId });
                    if (pdu.Length >= 2 && (pdu[0] & 0x80) != 0)
                    {
                        byte ex = pdu[1];
                        lastError = $"Modbus exception {ex:X2} ({SlaveExceptionName(ex)})";
                        LogError("2B ReadDeviceIdentification", objectId, 0, lastError);
                        if (attempt == attempts) break;
                        continue;
                    }
                    var parsed = ParseDeviceIdResponse(pdu);
                    LogRx("2B ReadDeviceIdentification", objectId, parsed.Objects.Count, $"objs={parsed.Objects.Count}");
                    return ModbusResult<DeviceIdentification>.Ok(parsed);
                }
                catch (Exception ex)
                {
                    lastError = Describe(ex);
                    LogError("2B ReadDeviceIdentification", objectId, 0, lastError);
                    if (attempt == attempts) break;
                }
            }
            return ModbusResult<DeviceIdentification>.Fail(lastError);
        }
    }

    /// <summary>
    /// FC 08 Diagnostics — sub-function 0x0000 "Return Query Data". The slave should echo back the
    /// data bytes; returning the same value confirms the link is alive. Useful as a heartbeat.
    /// </summary>
    public ModbusResult<ushort> Diagnostic(ushort subFunction = 0, ushort data = 0)
    {
        lock (_lock)
        {
            if (!Connected || _appliedSettings?.ConnectionMode != ConnectionMode.Tcp || _tcp is null)
                return ModbusResult<ushort>.Fail("Not connected (TCP only for now)");
            LogTx("08 Diagnostic", subFunction, 0, $"sub={subFunction:X4} data={data:X4}");
            try
            {
                var pdu = SendRawTcpPdu(new byte[] { 0x08, (byte)(subFunction >> 8), (byte)subFunction, (byte)(data >> 8), (byte)data });
                if (pdu.Length >= 2 && (pdu[0] & 0x80) != 0)
                    return Fail<ushort>("08 Diagnostic", pdu);
                ushort echo = (ushort)((pdu[3] << 8) | pdu[4]);
                LogRx("08 Diagnostic", subFunction, 0, $"echo={echo:X4}");
                return ModbusResult<ushort>.Ok(echo);
            }
            catch (Exception ex) { return ModbusResult<ushort>.Fail(Describe(ex)); }
        }
    }

    /// <summary>FC 11 Get Comm Event Counter — returns status word + event count.</summary>
    public ModbusResult<(ushort Status, ushort Count)> GetCommEventCounter()
    {
        lock (_lock)
        {
            if (!Connected || _appliedSettings?.ConnectionMode != ConnectionMode.Tcp || _tcp is null)
                return ModbusResult<(ushort, ushort)>.Fail("Not connected (TCP only for now)");
            LogTx("0B GetCommEventCounter", 0, 0);
            try
            {
                var pdu = SendRawTcpPdu(new byte[] { 0x0B });
                if (pdu.Length >= 2 && (pdu[0] & 0x80) != 0)
                    return Fail<(ushort, ushort)>("0B GetCommEventCounter", pdu);
                ushort status = (ushort)((pdu[1] << 8) | pdu[2]);
                ushort count  = (ushort)((pdu[3] << 8) | pdu[4]);
                LogRx("0B GetCommEventCounter", 0, 0, $"status={status:X4} count={count}");
                return ModbusResult<(ushort, ushort)>.Ok((status, count));
            }
            catch (Exception ex) { return ModbusResult<(ushort, ushort)>.Fail(Describe(ex)); }
        }
    }

    /// <summary>FC 17 Report Server ID — returns server identification bytes + run-indicator.</summary>
    public ModbusResult<(string Id, bool RunStatus)> ReportServerId()
    {
        lock (_lock)
        {
            if (!Connected || _appliedSettings?.ConnectionMode != ConnectionMode.Tcp || _tcp is null)
                return ModbusResult<(string, bool)>.Fail("Not connected (TCP only for now)");
            LogTx("11 ReportServerId", 0, 0);
            try
            {
                var pdu = SendRawTcpPdu(new byte[] { 0x11 });
                if (pdu.Length >= 2 && (pdu[0] & 0x80) != 0)
                    return Fail<(string, bool)>("11 ReportServerId", pdu);
                int byteCount = pdu[1];
                if (byteCount < 1 || 2 + byteCount > pdu.Length)
                    return ModbusResult<(string, bool)>.Fail("Malformed FC 17 response");
                string id = System.Text.Encoding.ASCII.GetString(pdu, 2, byteCount - 1);
                bool running = pdu[2 + byteCount - 1] == 0xFF;
                LogRx("11 ReportServerId", 0, 0, $"id={id} run={running}");
                return ModbusResult<(string, bool)>.Ok((id, running));
            }
            catch (Exception ex) { return ModbusResult<(string, bool)>.Fail(Describe(ex)); }
        }
    }

    private ModbusResult<T> Fail<T>(string label, byte[] exceptionPdu)
    {
        byte ex = exceptionPdu[1];
        string err = $"Modbus exception {ex:X2} ({SlaveExceptionName(ex)})";
        LogError(label, 0, 0, err);
        return ModbusResult<T>.Fail(err);
    }

    /// <summary>
    /// Test Center / raw-PDU escape hatch. Exposes the raw TCP send for diagnostics and protocol
    /// experimentation. Returns the PDU bytes (without MBAP wrapper). Caller is responsible for
    /// interpreting the response — exception responses (high bit of FC) come through verbatim.
    /// </summary>
    public ModbusResult<byte[]> SendRawPdu(byte[] pdu)
    {
        lock (_lock)
        {
            if (!Connected || _appliedSettings?.ConnectionMode != ConnectionMode.Tcp || _tcp is null)
                return ModbusResult<byte[]>.Fail("Not connected (TCP only)");
            LogTx("?? RawPdu", 0, pdu.Length, BitConverter.ToString(pdu).Replace("-", " "));
            try
            {
                var resp = SendRawTcpPdu(pdu);
                LogRx("?? RawPdu", 0, resp.Length, BitConverter.ToString(resp).Replace("-", " "));
                return ModbusResult<byte[]>.Ok(resp);
            }
            catch (Exception ex) { return ModbusResult<byte[]>.Fail(Describe(ex)); }
        }
    }

    private static int _txnId;
    private byte[] SendRawTcpPdu(byte[] pdu)
    {
        var stream = _tcp!.GetStream();
        ushort txn = (ushort)Interlocked.Increment(ref _txnId);
        int length = pdu.Length + 1; // unit id + PDU
        var frame = new byte[7 + pdu.Length];
        frame[0] = (byte)(txn >> 8);
        frame[1] = (byte)txn;
        frame[2] = 0; frame[3] = 0;
        frame[4] = (byte)(length >> 8);
        frame[5] = (byte)length;
        frame[6] = _unitId;
        Buffer.BlockCopy(pdu, 0, frame, 7, pdu.Length);

        int readTimeout = _master?.Transport?.ReadTimeout ?? 2000;
        int writeTimeout = _master?.Transport?.WriteTimeout ?? 2000;
        stream.WriteTimeout = writeTimeout;
        stream.ReadTimeout = readTimeout;
        stream.Write(frame, 0, frame.Length);

        var header = new byte[7];
        ReadExact(stream, header, 0, 7);
        int respLen = (header[4] << 8) | header[5];
        if (respLen < 2 || respLen > 254) throw new InvalidDataException($"Bad MBAP length {respLen}");
        var rest = new byte[respLen - 1];
        ReadExact(stream, rest, 0, rest.Length);
        return rest; // PDU only (header[6] is the unit id)
    }

    private static void ReadExact(System.IO.Stream s, byte[] buf, int off, int n)
    {
        int total = 0;
        while (total < n)
        {
            int read = s.Read(buf, off + total, n - total);
            if (read == 0) throw new IOException("Stream closed before PDU complete");
            total += read;
        }
    }

    private static DeviceIdentification ParseDeviceIdResponse(byte[] pdu)
    {
        // 0:FC 1:MEI 2:Code 3:Conformity 4:MoreFollows 5:NextObjectId 6:NumObjects 7..: objects
        if (pdu.Length < 7 || pdu[0] != 0x2B || pdu[1] != 0x0E)
            throw new InvalidDataException("Malformed FC 43 response");
        byte conformity = pdu[3];
        bool more = pdu[4] == 0xFF;
        byte nextId = pdu[5];
        int n = pdu[6];
        var objects = new System.Collections.Generic.List<DeviceIdObject>(n);
        int p = 7;
        for (int i = 0; i < n; i++)
        {
            if (p + 2 > pdu.Length) throw new InvalidDataException("Truncated FC 43 object header");
            byte id = pdu[p++];
            byte len = pdu[p++];
            if (p + len > pdu.Length) throw new InvalidDataException("Truncated FC 43 object value");
            string val = System.Text.Encoding.UTF8.GetString(pdu, p, len);
            p += len;
            objects.Add(new DeviceIdObject(id, val));
        }
        return new DeviceIdentification(conformity, more, nextId, objects);
    }

    private ModbusResult<T> Read<T>(string label, int addr, int qty, Func<IModbusMaster, T> op, Func<T, string>? summarize = null)
    {
        lock (_lock)
        {
            if (_master is null || !Connected)
            {
                LogError(label, addr, qty, "Not connected");
                return ModbusResult<T>.Fail("Not connected");
            }

            int attempts = Math.Max(1, 1 + RetriesSetting);
            string lastError = "Unknown";
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                LogTx(label, addr, qty, attempt > 1 ? $"(retry {attempt - 1})" : "");
                try
                {
                    var v = op(_master);
                    LogRx(label, addr, qty, summarize?.Invoke(v) ?? "");
                    return ModbusResult<T>.Ok(v);
                }
                catch (Exception ex)
                {
                    lastError = Describe(ex);
                    LogError(label, addr, qty, lastError);
                    if (attempt == attempts) break;
                }
            }
            return ModbusResult<T>.Fail(lastError);
        }
    }

    private ModbusResult Write(string label, int addr, int qty, Action<IModbusMaster> op, string detail = "")
    {
        lock (_lock)
        {
            if (_master is null || !Connected)
            {
                LogError(label, addr, qty, "Not connected");
                return ModbusResult.Fail("Not connected");
            }

            int attempts = Math.Max(1, 1 + RetriesSetting);
            string lastError = "Unknown";
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                LogTx(label, addr, qty, attempt > 1 ? $"{detail} (retry {attempt - 1})" : detail);
                try
                {
                    op(_master);
                    LogRx(label, addr, qty, "ack");
                    return ModbusResult.Ok();
                }
                catch (Exception ex)
                {
                    lastError = Describe(ex);
                    LogError(label, addr, qty, lastError);
                    if (attempt == attempts) break;
                }
            }
            return ModbusResult.Fail(lastError);
        }
    }

    private static void LogTx(string fn, int addr, int qty, string detail = "") =>
        TrafficLog.Record(new TrafficEvent { Direction = TrafficDirection.Tx, Function = fn, Address = addr, Quantity = qty, Detail = detail });
    private static void LogRx(string fn, int addr, int qty, string detail) =>
        TrafficLog.Record(new TrafficEvent { Direction = TrafficDirection.Rx, Function = fn, Address = addr, Quantity = qty, Detail = detail });
    private static void LogError(string fn, int addr, int qty, string detail) =>
        TrafficLog.Record(new TrafficEvent { Direction = TrafficDirection.Error, Function = fn, Address = addr, Quantity = qty, Detail = detail });

    private static string SummarizeBools(bool[] v)
    {
        var n = Math.Min(v.Length, 16);
        var s = string.Concat(new ReadOnlySpan<bool>(v, 0, n).ToArray().Select(b => b ? "1" : "0"));
        return v.Length > n ? $"[{s}…]" : $"[{s}]";
    }

    private static string SummarizeInts(int[] v)
    {
        var n = Math.Min(v.Length, 8);
        var s = string.Join(",", new ArraySegment<int>(v, 0, n).Select(i => i.ToString()));
        return v.Length > n ? $"[{s}…]" : $"[{s}]";
    }

    private void ApplyTunables(PollDefinition settings)
    {
        _unitId = (byte)(settings.NodeId & 0xFF);
        var responseTimeout = Math.Max(50, settings.ResponseTimeoutMs > 0 ? settings.ResponseTimeoutMs : settings.ConnectionTimeoutMs);
        if (_master is not null)
        {
            _master.Transport.ReadTimeout = responseTimeout;
            _master.Transport.WriteTimeout = responseTimeout;
            // We run our own retry loop so we can log each attempt — keep NModbus's count at 0
            // to avoid double-retry.
            _master.Transport.Retries = 0;
            // By default NModbus retries Slave-Busy (exception 06) and Acknowledge (05) responses
            // INFINITELY and ignores the Retries setting. Setting this true makes NModbus honour
            // Retries for those exceptions too — so a single Slave-Busy reply surfaces immediately.
            _master.Transport.SlaveBusyUsesRetryCount = true;
        }
        if (_serial is not null)
        {
            _serial.ReadTimeout = responseTimeout;
            _serial.WriteTimeout = responseTimeout;
        }
        if (_tcp is not null)
        {
            _tcp.ReceiveTimeout = responseTimeout;
            _tcp.SendTimeout = responseTimeout;
        }
    }

    private int RetriesSetting => _appliedSettings?.Retries ?? 0;

    private static bool SameTransport(PollDefinition a, PollDefinition b)
    {
        if (a.ConnectionMode != b.ConnectionMode) return false;
        return a.ConnectionMode == ConnectionMode.Tcp
            ? a.IpAddress == b.IpAddress && a.ServerPort == b.ServerPort
            : a.SerialPortName == b.SerialPortName
                && a.BaudRate == b.BaudRate
                && a.Parity == b.Parity
                && a.StopBits == b.StopBits;
    }

    private static string Describe(Exception ex) => ex switch
    {
        SlaveException sx => $"Modbus exception {sx.SlaveExceptionCode:X2} ({SlaveExceptionName(sx.SlaveExceptionCode)})",
        SocketException sox => "Network: " + sox.Message,
        TimeoutException => "Timeout",
        System.IO.IOException io => "I/O error: " + io.Message,
        UnauthorizedAccessException ua => "Permission denied: " + ua.Message,
        InvalidOperationException io => io.Message,
        _ => $"{ex.GetType().Name}: {ex.Message}"
    };

    private static string SlaveExceptionName(byte code) => code switch
    {
        1 => "Illegal function",
        2 => "Illegal data address",
        3 => "Illegal data value",
        4 => "Slave device failure",
        5 => "Acknowledge",
        6 => "Slave device busy",
        7 => "Negative acknowledge",
        8 => "Memory parity error",
        10 => "Gateway path unavailable",
        11 => "Gateway target failed to respond",
        _ => "Unknown",
    };

    public void Dispose() => Disconnect();
}
