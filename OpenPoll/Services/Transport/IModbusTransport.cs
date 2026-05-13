using System;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Wire-level Modbus transport. Hides the framing differences between TCP MBAP, UDP MBAP,
/// Serial RTU, RTU-over-TCP, ASCII-over-TCP, and TLS so the FC codec only needs to deal with
/// pure PDU bytes. <see cref="SendReceive"/> sends one request and waits for the matching
/// response (per the spec a slave returns exactly one response per non-broadcast request).
/// </summary>
public interface IModbusTransport : IDisposable
{
    /// <summary>True once the transport has a usable underlying connection.</summary>
    bool Connected { get; }

    /// <summary>Send a request PDU and return the response PDU bytes (without any framing wrapper).</summary>
    byte[] SendReceive(byte unitId, byte[] pdu);

    /// <summary>Per-request read/write timeouts (in ms). Updated each time the user changes settings.</summary>
    int ReadTimeoutMs { get; set; }
    int WriteTimeoutMs { get; set; }
}
