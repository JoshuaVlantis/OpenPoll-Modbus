using System;
using System.Linq;

namespace OpenPoll.Services.Transport;

/// <summary>
/// Build &amp; parse Modbus PDUs (function code + payload) independent of the underlying transport.
/// Pairs with <see cref="IModbusTransport"/>: this class produces the request bytes and reads the
/// response bytes; the transport handles framing/checksums.
/// </summary>
public static class ModbusPdu
{
    public static byte[] BuildReadBits(byte fc, int address, int quantity) =>
        new byte[] { fc, Hi(address), Lo(address), Hi(quantity), Lo(quantity) };

    public static byte[] BuildReadRegisters(byte fc, int address, int quantity) =>
        new byte[] { fc, Hi(address), Lo(address), Hi(quantity), Lo(quantity) };

    public static byte[] BuildWriteSingleCoil(int address, bool on)
    {
        var pdu = new byte[5];
        pdu[0] = 0x05;
        pdu[1] = Hi(address); pdu[2] = Lo(address);
        pdu[3] = (byte)(on ? 0xFF : 0x00); pdu[4] = 0x00;
        return pdu;
    }

    public static byte[] BuildWriteSingleRegister(int address, int value)
    {
        var pdu = new byte[5];
        pdu[0] = 0x06;
        pdu[1] = Hi(address); pdu[2] = Lo(address);
        pdu[3] = Hi(value);   pdu[4] = Lo(value);
        return pdu;
    }

    public static byte[] BuildWriteMultipleCoils(int address, bool[] values)
    {
        int byteCount = (values.Length + 7) / 8;
        var pdu = new byte[6 + byteCount];
        pdu[0] = 0x0F;
        pdu[1] = Hi(address); pdu[2] = Lo(address);
        pdu[3] = Hi(values.Length); pdu[4] = Lo(values.Length);
        pdu[5] = (byte)byteCount;
        for (int i = 0; i < values.Length; i++)
            if (values[i]) pdu[6 + i / 8] |= (byte)(1 << (i % 8));
        return pdu;
    }

    public static byte[] BuildWriteMultipleRegisters(int address, int[] values)
    {
        int byteCount = values.Length * 2;
        var pdu = new byte[6 + byteCount];
        pdu[0] = 0x10;
        pdu[1] = Hi(address); pdu[2] = Lo(address);
        pdu[3] = Hi(values.Length); pdu[4] = Lo(values.Length);
        pdu[5] = (byte)byteCount;
        for (int i = 0; i < values.Length; i++)
        {
            pdu[6 + i * 2]     = Hi(values[i]);
            pdu[6 + i * 2 + 1] = Lo(values[i]);
        }
        return pdu;
    }

    public static byte[] BuildMaskWriteRegister(int address, ushort andMask, ushort orMask)
    {
        var pdu = new byte[7];
        pdu[0] = 0x16;
        pdu[1] = Hi(address); pdu[2] = Lo(address);
        pdu[3] = Hi(andMask); pdu[4] = Lo(andMask);
        pdu[5] = Hi(orMask);  pdu[6] = Lo(orMask);
        return pdu;
    }

    public static byte[] BuildReadWriteMultipleRegisters(int readAddress, int readQuantity, int writeAddress, int[] writeValues)
    {
        int byteCount = writeValues.Length * 2;
        var pdu = new byte[10 + byteCount];
        pdu[0] = 0x17;
        pdu[1] = Hi(readAddress);  pdu[2] = Lo(readAddress);
        pdu[3] = Hi(readQuantity); pdu[4] = Lo(readQuantity);
        pdu[5] = Hi(writeAddress); pdu[6] = Lo(writeAddress);
        pdu[7] = Hi(writeValues.Length); pdu[8] = Lo(writeValues.Length);
        pdu[9] = (byte)byteCount;
        for (int i = 0; i < writeValues.Length; i++)
        {
            pdu[10 + i * 2]     = Hi(writeValues[i]);
            pdu[10 + i * 2 + 1] = Lo(writeValues[i]);
        }
        return pdu;
    }

    public static byte[] BuildReadDeviceIdentification(byte code, byte objectId) =>
        new byte[] { 0x2B, 0x0E, code, objectId };

    public static byte[] BuildDiagnostic(ushort subFunction, ushort data) =>
        new byte[] { 0x08, Hi(subFunction), Lo(subFunction), Hi(data), Lo(data) };

    public static byte[] BuildGetCommEventCounter() => new byte[] { 0x0B };

    public static byte[] BuildReportServerId() => new byte[] { 0x11 };

    /// <summary>
    /// Inspect a response PDU. Throws <see cref="ModbusProtocolException"/> with the embedded
    /// exception code if the slave returned an error response (high bit of FC set).
    /// </summary>
    public static void ThrowIfException(byte[] pdu)
    {
        if (pdu.Length >= 2 && (pdu[0] & 0x80) != 0)
            throw new ModbusProtocolException(pdu[0] & 0x7F, pdu[1]);
    }

    /// <summary>Decode a Read-Coils / Read-Discrete-Inputs response into a bool array of <paramref name="quantity"/> bits.</summary>
    public static bool[] ParseReadBits(byte[] pdu, int quantity)
    {
        ThrowIfException(pdu);
        if (pdu.Length < 2) throw new InvalidOperationException("Truncated response");
        int byteCount = pdu[1];
        if (pdu.Length < 2 + byteCount) throw new InvalidOperationException("Truncated response payload");
        var bits = new bool[quantity];
        for (int i = 0; i < quantity; i++)
            bits[i] = (pdu[2 + i / 8] & (1 << (i % 8))) != 0;
        return bits;
    }

    public static int[] ParseReadRegisters(byte[] pdu)
    {
        ThrowIfException(pdu);
        if (pdu.Length < 2) throw new InvalidOperationException("Truncated response");
        int byteCount = pdu[1];
        if (pdu.Length < 2 + byteCount) throw new InvalidOperationException("Truncated response payload");
        int n = byteCount / 2;
        var regs = new int[n];
        for (int i = 0; i < n; i++)
            regs[i] = (pdu[2 + i * 2] << 8) | pdu[2 + i * 2 + 1];
        return regs;
    }

    private static byte Hi(int v) => (byte)((v >> 8) & 0xFF);
    private static byte Lo(int v) => (byte)(v & 0xFF);
}

/// <summary>Raised when the slave responds with a Modbus exception PDU (FC high bit set).</summary>
public sealed class ModbusProtocolException : Exception
{
    public int FunctionCode { get; }
    public byte ExceptionCode { get; }
    public ModbusProtocolException(int fc, byte code)
        : base($"Modbus exception {code:X2} ({ExceptionName(code)})")
    {
        FunctionCode = fc;
        ExceptionCode = code;
    }

    public static string ExceptionName(byte code) => code switch
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
}
