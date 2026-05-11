namespace OpenPoll.Models;

public enum ModbusFunction
{
    Coils = 0,
    DiscreteInputs = 1,
    HoldingRegisters = 2,
    InputRegisters = 3
}

public static class ModbusFunctionExtensions
{
    public static string Prefix(this ModbusFunction function) => function switch
    {
        ModbusFunction.Coils => "0x",
        ModbusFunction.DiscreteInputs => "1x",
        ModbusFunction.InputRegisters => "3x",
        ModbusFunction.HoldingRegisters => "4x",
        _ => "?"
    };

    public static bool IsRegister(this ModbusFunction function) =>
        function is ModbusFunction.HoldingRegisters or ModbusFunction.InputRegisters;

    public static bool IsWritable(this ModbusFunction function) =>
        function is ModbusFunction.Coils or ModbusFunction.HoldingRegisters;
}
