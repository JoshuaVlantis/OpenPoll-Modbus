namespace OpenSlave.Models;

public enum SlaveTableKind
{
    Coils,
    DiscreteInputs,
    HoldingRegisters,
    InputRegisters,
}

public static class SlaveTableKindExtensions
{
    public static string ShortLabel(this SlaveTableKind k) => k switch
    {
        SlaveTableKind.Coils => "0x",
        SlaveTableKind.DiscreteInputs => "1x",
        SlaveTableKind.HoldingRegisters => "4x",
        SlaveTableKind.InputRegisters => "3x",
        _ => "?"
    };

    public static string LongLabel(this SlaveTableKind k) => k switch
    {
        SlaveTableKind.Coils => "Coils",
        SlaveTableKind.DiscreteInputs => "Discrete Inputs",
        SlaveTableKind.HoldingRegisters => "Holding Registers",
        SlaveTableKind.InputRegisters => "Input Registers",
        _ => k.ToString()
    };

    public static bool IsBoolean(this SlaveTableKind k) =>
        k is SlaveTableKind.Coils or SlaveTableKind.DiscreteInputs;
}
