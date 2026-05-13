namespace OpenSlave.Services;

/// <summary>
/// CRC-16/MODBUS implementation per the Modbus RTU spec (poly 0xA001, init 0xFFFF, byte-reversed
/// output). Tiny and standalone so the slave's serial transport can compute frame checksums
/// without pulling in NModbus on the server side.
/// </summary>
public static class ModbusCrc
{
    /// <summary>Compute the 16-bit CRC over <paramref name="length"/> bytes of <paramref name="buf"/> starting at <paramref name="offset"/>.</summary>
    public static ushort Compute(byte[] buf, int offset, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = 0; i < length; i++)
        {
            crc ^= buf[offset + i];
            for (int b = 0; b < 8; b++)
            {
                if ((crc & 0x0001) != 0) crc = (ushort)((crc >> 1) ^ 0xA001);
                else                      crc >>= 1;
            }
        }
        return crc;
    }

    /// <summary>Wrap a Modbus PDU as an RTU frame: <c>SlaveId · PDU · CRC_lo · CRC_hi</c>.</summary>
    public static byte[] WrapRtu(byte slaveId, byte[] pdu)
    {
        var frame = new byte[1 + pdu.Length + 2];
        frame[0] = slaveId;
        System.Buffer.BlockCopy(pdu, 0, frame, 1, pdu.Length);
        ushort crc = Compute(frame, 0, frame.Length - 2);
        frame[^2] = (byte)(crc & 0xFF);
        frame[^1] = (byte)(crc >> 8);
        return frame;
    }

    /// <summary>True iff the trailing two bytes are a valid CRC over the preceding bytes.</summary>
    public static bool Verify(byte[] buf, int offset, int length)
    {
        if (length < 3) return false;
        ushort expected = Compute(buf, offset, length - 2);
        ushort got = (ushort)(buf[offset + length - 2] | (buf[offset + length - 1] << 8));
        return expected == got;
    }
}
