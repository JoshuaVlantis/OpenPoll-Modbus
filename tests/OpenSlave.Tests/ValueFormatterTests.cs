using FluentAssertions;
using OpenSlave.Models;
using OpenSlave.Services;

namespace OpenSlave.Tests;

public sealed class ValueFormatterTests
{
    [Fact]
    public void FormatRegister_Signed16_FormatsTwoComplement()
    {
        ValueFormatter.FormatRegister(0xFFFF, CellDataType.Signed).Should().Be("-1");
        ValueFormatter.FormatRegister(0x7FFF, CellDataType.Signed).Should().Be("32767");
        ValueFormatter.FormatRegister(0x8000, CellDataType.Signed).Should().Be("-32768");
    }

    [Fact]
    public void FormatRegister_Unsigned16_FormatsRange()
    {
        ValueFormatter.FormatRegister(0x0000, CellDataType.Unsigned).Should().Be("0");
        ValueFormatter.FormatRegister(0xFFFF, CellDataType.Unsigned).Should().Be("65535");
    }

    [Fact]
    public void FormatRegister_Hex16_HasPrefixAndPadding()
    {
        ValueFormatter.FormatRegister(0x0042, CellDataType.Hex).Should().Be("0x0042");
        ValueFormatter.FormatRegister(0xFFFF, CellDataType.Hex).Should().Be("0xFFFF");
    }

    [Fact]
    public void FormatRegister_Binary16_GroupsInNibbles()
    {
        ValueFormatter.FormatRegister(0xFF00, CellDataType.Binary).Should().Be("1111 1111 0000 0000");
    }

    [Theory]
    [InlineData(WordOrder.BigEndian,            0x1234, 0x5678, "305419896")] // 0x12345678
    [InlineData(WordOrder.LittleEndian,         0x1234, 0x5678, "1450709556")] // 0x56781234
    [InlineData(WordOrder.BigEndianByteSwap,    0x1234, 0x5678, "873625686")]  // 0x34127856
    [InlineData(WordOrder.LittleEndianByteSwap, 0x1234, 0x5678, "2018915346")] // 0x78563412
    public void FormatRegister_Unsigned32_HonoursWordOrder(WordOrder order, int w0, int w1, string expected)
    {
        ValueFormatter.FormatRegister(new[] { w0, w1 }, CellDataType.Unsigned32, order).Should().Be(expected);
    }

    [Fact]
    public void FormatRegister_Float_RoundtripsBigEndian()
    {
        // 1.0f = 0x3F800000
        var formatted = ValueFormatter.FormatRegister(new[] { 0x3F80, 0x0000 }, CellDataType.Float, WordOrder.BigEndian);
        formatted.Should().Be("1");
    }

    [Fact]
    public void FormatRegister_Double_RoundtripsBigEndian()
    {
        // 3.14 = 0x40091EB851EB851F
        var formatted = ValueFormatter.FormatRegister(new[] { 0x4009, 0x1EB8, 0x51EB, 0x851F }, CellDataType.Double, WordOrder.BigEndian);
        formatted.Should().Be("3.1400000000000001");
    }

    [Theory]
    [InlineData("42", CellDataType.Unsigned, 42)]
    [InlineData("-1", CellDataType.Signed, 0xFFFF)]
    [InlineData("0xABCD", CellDataType.Hex, 0xABCD)]
    [InlineData("1010 1010", CellDataType.Binary, 0xAA)]
    public void TryParseRegister_ParsesCommonFormats(string text, CellDataType type, int expected)
    {
        ValueFormatter.TryParseRegister(text, type, out var raw).Should().BeTrue();
        raw.Should().Be(expected);
    }

    [Fact]
    public void TryParseMultiRegister_Float_RoundTrips()
    {
        ValueFormatter.TryParseMultiRegister("1.0", CellDataType.Float, WordOrder.BigEndian, out var words).Should().BeTrue();
        words.Should().Equal(0x3F80, 0x0000);
    }

    [Fact]
    public void TryParseCoil_AcceptsCommonForms()
    {
        ValueFormatter.TryParseCoil("1", out var v).Should().BeTrue(); v.Should().BeTrue();
        ValueFormatter.TryParseCoil("true", out v).Should().BeTrue(); v.Should().BeTrue();
        ValueFormatter.TryParseCoil("on", out v).Should().BeTrue(); v.Should().BeTrue();
        ValueFormatter.TryParseCoil("0", out v).Should().BeTrue(); v.Should().BeFalse();
        ValueFormatter.TryParseCoil("garbage", out _).Should().BeFalse();
    }
}
