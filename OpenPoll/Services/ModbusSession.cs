using System;
using System.Linq;
using EasyModbus;
using OpenPoll.Models;

namespace OpenPoll.Services;

public sealed class ModbusSession : IDisposable
{
    private readonly object _lock = new();
    private ModbusClient? _client;
    private PollDefinition? _appliedSettings;

    public bool Connected
    {
        get
        {
            lock (_lock)
            {
                return _client?.Connected ?? false;
            }
        }
    }

    public ModbusResult Connect(PollDefinition settings)
    {
        lock (_lock)
        {
            try
            {
                if (_client is { Connected: true } &&
                    _appliedSettings is not null &&
                    SameTransport(_appliedSettings, settings))
                {
                    ApplyTunables(_client, settings);
                    return ModbusResult.Ok();
                }

                if (_client is not null)
                {
                    SafeDisconnect(_client);
                    _client = null;
                }

                var client = settings.ConnectionMode == ConnectionMode.Tcp
                    ? new ModbusClient(settings.IpAddress, settings.ServerPort)
                    : new ModbusClient(settings.SerialPortName)
                    {
                        Baudrate = settings.BaudRate,
                        Parity = settings.Parity,
                        StopBits = settings.StopBits
                    };

                ApplyTunables(client, settings);
                client.Connect();

                _client = client;
                _appliedSettings = Clone(settings);
                return ModbusResult.Ok();
            }
            catch (Exception ex)
            {
                return ModbusResult.Fail(Describe(ex));
            }
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            if (_client is null) return;
            SafeDisconnect(_client);
            _client = null;
            _appliedSettings = null;
        }
    }

    public ModbusResult<bool[]> ReadCoils(int address, int quantity) =>
        Read("01 ReadCoils", address, quantity, c => c.ReadCoils(address, quantity), v => SummarizeBools(v));

    public ModbusResult<bool[]> ReadDiscreteInputs(int address, int quantity) =>
        Read("02 ReadDiscreteInputs", address, quantity, c => c.ReadDiscreteInputs(address, quantity), v => SummarizeBools(v));

    public ModbusResult<int[]> ReadHoldingRegisters(int address, int quantity) =>
        Read("03 ReadHoldingRegisters", address, quantity, c => c.ReadHoldingRegisters(address, quantity), v => SummarizeInts(v));

    public ModbusResult<int[]> ReadInputRegisters(int address, int quantity) =>
        Read("04 ReadInputRegisters", address, quantity, c => c.ReadInputRegisters(address, quantity), v => SummarizeInts(v));

    public ModbusResult WriteSingleCoil(int address, bool value) =>
        Write("05 WriteSingleCoil", address, 1, c => c.WriteSingleCoil(address, value), $"= {(value ? "1" : "0")}");

    public ModbusResult WriteSingleRegister(int address, int value) =>
        Write("06 WriteSingleRegister", address, 1, c => c.WriteSingleRegister(address, value), $"= {value}");

    public ModbusResult WriteMultipleCoils(int address, bool[] values) =>
        Write("0F WriteMultipleCoils", address, values.Length, c => c.WriteMultipleCoils(address, values), SummarizeBools(values));

    public ModbusResult WriteMultipleRegisters(int address, int[] values) =>
        Write("10 WriteMultipleRegisters", address, values.Length, c => c.WriteMultipleRegisters(address, values), SummarizeInts(values));

    private ModbusResult<T> Read<T>(string label, int addr, int qty, Func<ModbusClient, T> op, Func<T, string>? summarize = null)
    {
        lock (_lock)
        {
            if (_client is null || !_client.Connected)
            {
                LogError(label, addr, qty, "Not connected");
                return ModbusResult<T>.Fail("Not connected");
            }
            LogTx(label, addr, qty);
            try
            {
                var v = op(_client);
                LogRx(label, addr, qty, summarize?.Invoke(v) ?? "");
                return ModbusResult<T>.Ok(v);
            }
            catch (Exception ex)
            {
                var d = Describe(ex);
                LogError(label, addr, qty, d);
                return ModbusResult<T>.Fail(d);
            }
        }
    }

    private ModbusResult Write(string label, int addr, int qty, Action<ModbusClient> op, string detail = "")
    {
        lock (_lock)
        {
            if (_client is null || !_client.Connected)
            {
                LogError(label, addr, qty, "Not connected");
                return ModbusResult.Fail("Not connected");
            }
            LogTx(label, addr, qty, detail);
            try
            {
                op(_client);
                LogRx(label, addr, qty, "ack");
                return ModbusResult.Ok();
            }
            catch (Exception ex)
            {
                var d = Describe(ex);
                LogError(label, addr, qty, d);
                return ModbusResult.Fail(d);
            }
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

    private static void ApplyTunables(ModbusClient client, PollDefinition settings)
    {
        client.UnitIdentifier = (byte)(settings.NodeId & 0xFF);
        client.ConnectionTimeout = settings.ConnectionTimeoutMs;
    }

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

    private static PollDefinition Clone(PollDefinition s) => s.Clone();

    private static void SafeDisconnect(ModbusClient client)
    {
        try { client.Disconnect(); } catch { }
    }

    private static string Describe(Exception ex) => ex switch
    {
        EasyModbus.Exceptions.ConnectionException => "Connection exception",
        EasyModbus.Exceptions.CRCCheckFailedException => "CRC check failed",
        EasyModbus.Exceptions.FunctionCodeNotSupportedException => "Function code not supported",
        EasyModbus.Exceptions.QuantityInvalidException => "Quantity invalid",
        EasyModbus.Exceptions.SerialPortNotOpenedException => "Serial port not opened",
        EasyModbus.Exceptions.StartingAddressInvalidException => "Illegal data address",
        System.Net.Sockets.SocketException sx => "Network: " + sx.Message,
        System.IO.IOException => "Node ID error",
        TimeoutException => "Timeout",
        UnauthorizedAccessException ua => "Permission denied: " + ua.Message,
        _ => $"{ex.GetType().Name}: {ex.Message}"
    };

    public void Dispose() => Disconnect();
}
