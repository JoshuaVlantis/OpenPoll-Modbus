using FluentAssertions;
using OpenPoll.Models;

namespace OpenPoll.Tests;

public class CellDataTypeTests
{
    [Theory]
    [InlineData(CellDataType.Signed, 1)]
    [InlineData(CellDataType.Unsigned, 1)]
    [InlineData(CellDataType.Hex, 1)]
    [InlineData(CellDataType.Binary, 1)]
    [InlineData(CellDataType.Signed32, 2)]
    [InlineData(CellDataType.Unsigned32, 2)]
    [InlineData(CellDataType.Hex32, 2)]
    [InlineData(CellDataType.Float, 2)]
    [InlineData(CellDataType.Signed64, 4)]
    [InlineData(CellDataType.Unsigned64, 4)]
    [InlineData(CellDataType.Hex64, 4)]
    [InlineData(CellDataType.Double, 4)]
    public void WordCount_MatchesType(CellDataType t, int expected)
    {
        t.WordCount().Should().Be(expected);
    }

    [Theory]
    [InlineData(ModbusFunction.Coils, "0x")]
    [InlineData(ModbusFunction.DiscreteInputs, "1x")]
    [InlineData(ModbusFunction.HoldingRegisters, "4x")]
    [InlineData(ModbusFunction.InputRegisters, "3x")]
    public void ModbusFunction_PrefixMatchesPlcConvention(ModbusFunction f, string expected)
    {
        f.Prefix().Should().Be(expected);
    }

    [Theory]
    [InlineData(ModbusFunction.Coils, true)]
    [InlineData(ModbusFunction.DiscreteInputs, false)]
    [InlineData(ModbusFunction.HoldingRegisters, true)]
    [InlineData(ModbusFunction.InputRegisters, false)]
    public void IsWritable_OnlyTrueForCoilsAndHoldingRegisters(ModbusFunction f, bool expected)
    {
        f.IsWritable().Should().Be(expected);
    }

    [Theory]
    [InlineData(ModbusFunction.Coils, false)]
    [InlineData(ModbusFunction.DiscreteInputs, false)]
    [InlineData(ModbusFunction.HoldingRegisters, true)]
    [InlineData(ModbusFunction.InputRegisters, true)]
    public void IsRegister_OnlyTrueForRegisterFunctions(ModbusFunction f, bool expected)
    {
        f.IsRegister().Should().Be(expected);
    }
}
