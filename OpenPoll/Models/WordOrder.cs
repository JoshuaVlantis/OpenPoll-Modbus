namespace OpenPoll.Models;

/// <summary>
/// Word and byte order for multi-register data types (32-bit and 64-bit values).
/// Different PLC vendors use different conventions; this lets users pick.
/// </summary>
public enum WordOrder
{
    /// <summary>Big-endian: high word first, high byte first within word. Standard Modbus.</summary>
    BigEndian = 0,

    /// <summary>Little-endian: low word first, low byte first within word.</summary>
    LittleEndian = 1,

    /// <summary>Big-endian word order, little-endian byte order within word.</summary>
    BigEndianByteSwap = 2,

    /// <summary>Little-endian word order, big-endian byte order within word.</summary>
    LittleEndianByteSwap = 3,
}
