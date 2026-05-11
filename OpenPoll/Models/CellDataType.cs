namespace OpenPoll.Models;

public enum CellDataType
{
    // 16-bit (1 register) — original four kept for backward compatibility
    Signed = 0,
    Unsigned = 1,
    Hex = 2,
    Binary = 3,

    // 32-bit (2 registers)
    Signed32 = 10,
    Unsigned32 = 11,
    Hex32 = 12,
    Float = 13,

    // 64-bit (4 registers)
    Signed64 = 20,
    Unsigned64 = 21,
    Hex64 = 22,
    Double = 23,
}

public static class CellDataTypeExtensions
{
    public static int WordCount(this CellDataType type) => type switch
    {
        CellDataType.Signed64 or CellDataType.Unsigned64
            or CellDataType.Hex64 or CellDataType.Double => 4,
        CellDataType.Signed32 or CellDataType.Unsigned32
            or CellDataType.Hex32 or CellDataType.Float => 2,
        _ => 1
    };

    public static string Label(this CellDataType type) => type switch
    {
        CellDataType.Signed       => "Signed (16)",
        CellDataType.Unsigned     => "Unsigned (16)",
        CellDataType.Hex          => "Hex (16)",
        CellDataType.Binary       => "Binary (16)",
        CellDataType.Signed32     => "Signed (32)",
        CellDataType.Unsigned32   => "Unsigned (32)",
        CellDataType.Hex32        => "Hex (32)",
        CellDataType.Float        => "Float (32)",
        CellDataType.Signed64     => "Signed (64)",
        CellDataType.Unsigned64   => "Unsigned (64)",
        CellDataType.Hex64        => "Hex (64)",
        CellDataType.Double       => "Double (64)",
        _ => type.ToString()
    };
}
