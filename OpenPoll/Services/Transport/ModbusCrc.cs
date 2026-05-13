namespace OpenPoll.Services.Transport;

/// <summary>
/// CRC-16/MODBUS (poly 0xA001, init 0xFFFF, byte-reversed output). Duplicated from
/// <c>OpenSlave.Services.ModbusCrc</c> to keep the two projects free of cross-references — if a
/// third consumer ever appears, extract a shared <c>Modbus.Shared</c> library and remove both copies.
/// </summary>
public static class ModbusCrc
{
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

    public static bool Verify(byte[] buf, int offset, int length)
    {
        if (length < 3) return false;
        ushort expected = Compute(buf, offset, length - 2);
        ushort got = (ushort)(buf[offset + length - 2] | (buf[offset + length - 1] << 8));
        return expected == got;
    }
}
