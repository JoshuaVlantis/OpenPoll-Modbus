using System.IO.Ports;

namespace OpenPoll.Models;

/// <summary>
/// Configuration for a single Modbus poll: connection details + register range + function.
/// Several may exist concurrently in one workspace (one per tab).
/// </summary>
public sealed class PollDefinition
{
    public string Name { get; set; } = "";

    public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Tcp;

    public string IpAddress { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = 502;

    /// <summary>Time to wait for the TCP connect / serial-port open to succeed.</summary>
    public int ConnectionTimeoutMs { get; set; } = 1000;

    /// <summary>Per-request response timeout, separate from <see cref="ConnectionTimeoutMs"/>.</summary>
    public int ResponseTimeoutMs { get; set; } = 1000;

    /// <summary>Number of additional attempts after a transient failure before giving up.</summary>
    public int Retries { get; set; }

    public string SerialPortName { get; set; } = "";
    public int BaudRate { get; set; } = 9600;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;

    public int NodeId { get; set; } = 1;
    public int Address { get; set; } = 0;
    public int Amount { get; set; } = 10;
    public ModbusFunction Function { get; set; } = ModbusFunction.HoldingRegisters;
    public int PollingRateMs { get; set; } = 1000;

    /// <summary>0-indexed (raw protocol) or 1-indexed (PLC display) addresses.</summary>
    public bool DisplayOneIndexed { get; set; } = false;

    /// <summary>Word/byte order for multi-register data types (Phase B).</summary>
    public WordOrder WordOrder { get; set; } = WordOrder.BigEndian;

    public PollDefinition Clone() => new()
    {
        Name = Name,
        ConnectionMode = ConnectionMode,
        IpAddress = IpAddress,
        ServerPort = ServerPort,
        ConnectionTimeoutMs = ConnectionTimeoutMs,
        ResponseTimeoutMs = ResponseTimeoutMs,
        Retries = Retries,
        SerialPortName = SerialPortName,
        BaudRate = BaudRate,
        Parity = Parity,
        StopBits = StopBits,
        NodeId = NodeId,
        Address = Address,
        Amount = Amount,
        Function = Function,
        PollingRateMs = PollingRateMs,
        DisplayOneIndexed = DisplayOneIndexed,
        WordOrder = WordOrder,
    };
}
