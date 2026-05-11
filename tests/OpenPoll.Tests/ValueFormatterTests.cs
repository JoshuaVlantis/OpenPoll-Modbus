using FluentAssertions;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Tests;

public class ValueFormatterTests
{
    // ─────────── 16-bit format ─────────────────────────────────────────

    [Theory]
    [InlineData(0, CellDataType.Signed, "0")]
    [InlineData(1, CellDataType.Signed, "1")]
    [InlineData(-1, CellDataType.Signed, "-1")]            // 0xFFFF as int → -1 signed
    [InlineData(32767, CellDataType.Signed, "32767")]
    [InlineData(-32768, CellDataType.Signed, "-32768")]
    [InlineData(0, CellDataType.Unsigned, "0")]
    [InlineData(65535, CellDataType.Unsigned, "65535")]
    [InlineData(-1, CellDataType.Unsigned, "65535")]       // wire 0xFFFF → 65535 unsigned
    [InlineData(0, CellDataType.Hex, "0x0000")]
    [InlineData(255, CellDataType.Hex, "0x00FF")]
    [InlineData(65535, CellDataType.Hex, "0xFFFF")]
    [InlineData(0, CellDataType.Binary, "0000 0000 0000 0000")]
    [InlineData(0xAAAA, CellDataType.Binary, "1010 1010 1010 1010")]
    [InlineData(0xFFFF, CellDataType.Binary, "1111 1111 1111 1111")]
    public void Format16_ProducesExpectedString(int raw, CellDataType type, string expected)
    {
        ValueFormatter.FormatRegister(raw, type).Should().Be(expected);
    }

    // ─────────── 16-bit parse ──────────────────────────────────────────

    [Theory]
    [InlineData("0", CellDataType.Signed, 0)]
    [InlineData("32767", CellDataType.Signed, 32767)]
    [InlineData("-1", CellDataType.Signed, 65535)]         // -1 short → 0xFFFF wire
    [InlineData("-32768", CellDataType.Signed, 32768)]     // 0x8000
    [InlineData("65535", CellDataType.Unsigned, 65535)]
    [InlineData("0xFFFF", CellDataType.Hex, 65535)]
    [InlineData("0xff", CellDataType.Hex, 255)]
    [InlineData("FFFF", CellDataType.Hex, 65535)]
    [InlineData("0", CellDataType.Binary, 0)]
    [InlineData("1010 1010 1010 1010", CellDataType.Binary, 0xAAAA)]
    [InlineData("1111111111111111", CellDataType.Binary, 0xFFFF)]
    public void Parse16_ProducesWireValue(string text, CellDataType type, int expected)
    {
        ValueFormatter.TryParseRegister(text, type, out var raw).Should().BeTrue();
        raw.Should().Be(expected);
    }

    [Theory]
    [InlineData("", CellDataType.Signed)]
    [InlineData("   ", CellDataType.Unsigned)]
    [InlineData("not-a-number", CellDataType.Signed)]
    [InlineData("65536", CellDataType.Unsigned)]
    [InlineData("32768", CellDataType.Signed)]
    [InlineData("0xZZZZ", CellDataType.Hex)]
    [InlineData("0x10000", CellDataType.Hex)]
    [InlineData("12345", CellDataType.Binary)]
    [InlineData("11111111111111111", CellDataType.Binary)] // 17 bits
    public void Parse16_RejectsBadInput(string text, CellDataType type)
    {
        ValueFormatter.TryParseRegister(text, type, out _).Should().BeFalse();
    }

    // ─────────── 32-bit format & parse, all four word orders ──────────

    public static IEnumerable<object[]> WordOrders => new[]
    {
        new object[] { WordOrder.BigEndian },
        new object[] { WordOrder.LittleEndian },
        new object[] { WordOrder.BigEndianByteSwap },
        new object[] { WordOrder.LittleEndianByteSwap },
    };

    [Theory, MemberData(nameof(WordOrders))]
    public void Format32Float_RoundTripsThroughParse(WordOrder order)
    {
        foreach (var f in new[] { 0f, 1f, -1f, 3.14159f, -1234567.89f, 1e-30f, 1e30f })
        {
            ValueFormatter.TryParseMultiRegister(
                f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                CellDataType.Float, order, out var words).Should().BeTrue();
            var rendered = ValueFormatter.FormatRegister(words, CellDataType.Float, order);
            float.Parse(rendered, System.Globalization.CultureInfo.InvariantCulture)
                .Should().BeApproximately(f, MathF.Abs(f) * 1e-6f + 1e-30f);
        }
    }

    [Theory, MemberData(nameof(WordOrders))]
    public void Format32SignedRoundTrips(WordOrder order)
    {
        foreach (var v in new[] { 0, 1, -1, int.MinValue, int.MaxValue, -123456789, 999999 })
        {
            ValueFormatter.TryParseMultiRegister(
                v.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CellDataType.Signed32, order, out var words).Should().BeTrue();
            words.Length.Should().Be(2);
            var rendered = ValueFormatter.FormatRegister(words, CellDataType.Signed32, order);
            int.Parse(rendered, System.Globalization.CultureInfo.InvariantCulture).Should().Be(v);
        }
    }

    [Theory, MemberData(nameof(WordOrders))]
    public void Format32UnsignedRoundTrips(WordOrder order)
    {
        foreach (var v in new uint[] { 0, 1, uint.MaxValue, 12345678, 0xDEADBEEF })
        {
            ValueFormatter.TryParseMultiRegister(
                v.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CellDataType.Unsigned32, order, out var words).Should().BeTrue();
            var rendered = ValueFormatter.FormatRegister(words, CellDataType.Unsigned32, order);
            uint.Parse(rendered, System.Globalization.CultureInfo.InvariantCulture).Should().Be(v);
        }
    }

    [Theory, MemberData(nameof(WordOrders))]
    public void Format32HexRoundTrips(WordOrder order)
    {
        foreach (var v in new uint[] { 0, 1, 0xDEADBEEF, 0xFFFFFFFF, 0x12345678 })
        {
            ValueFormatter.TryParseMultiRegister(
                "0x" + v.ToString("X8"), CellDataType.Hex32, order, out var words).Should().BeTrue();
            var rendered = ValueFormatter.FormatRegister(words, CellDataType.Hex32, order);
            rendered.Should().Be("0x" + v.ToString("X8"));
        }
    }

    [Fact]
    public void Format32_BigEndianMatchesWireConvention()
    {
        // 0x12345678 in BigEndian (ABCD) should be reg[0] = 0x1234, reg[1] = 0x5678
        ValueFormatter.TryParseMultiRegister("0x12345678", CellDataType.Hex32, WordOrder.BigEndian, out var w).Should().BeTrue();
        w.Should().Equal(new int[] { 0x1234, 0x5678 });
    }

    [Fact]
    public void Format32_LittleEndianSwapsWords()
    {
        ValueFormatter.TryParseMultiRegister("0x12345678", CellDataType.Hex32, WordOrder.LittleEndian, out var w).Should().BeTrue();
        w.Should().Equal(new int[] { 0x5678, 0x1234 });
    }

    [Fact]
    public void Format32_BigEndianByteSwap_SwapsBytesWithinWord()
    {
        ValueFormatter.TryParseMultiRegister("0x12345678", CellDataType.Hex32, WordOrder.BigEndianByteSwap, out var w).Should().BeTrue();
        w.Should().Equal(new int[] { 0x3412, 0x7856 });
    }

    [Fact]
    public void Format32_LittleEndianByteSwap_FullySwapped()
    {
        ValueFormatter.TryParseMultiRegister("0x12345678", CellDataType.Hex32, WordOrder.LittleEndianByteSwap, out var w).Should().BeTrue();
        w.Should().Equal(new int[] { 0x7856, 0x3412 });
    }

    // ─────────── 64-bit ────────────────────────────────────────────────

    [Theory, MemberData(nameof(WordOrders))]
    public void Format64DoubleRoundTrips(WordOrder order)
    {
        foreach (var v in new[] { 0d, 1d, -1d, 3.141592653589793d, double.MaxValue, double.MinValue, 1e-300d, 1e300d })
        {
            ValueFormatter.TryParseMultiRegister(
                v.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                CellDataType.Double, order, out var words).Should().BeTrue();
            words.Length.Should().Be(4);
            var rendered = ValueFormatter.FormatRegister(words, CellDataType.Double, order);
            double.Parse(rendered, System.Globalization.CultureInfo.InvariantCulture)
                .Should().BeApproximately(v, System.Math.Abs(v) * 1e-12 + 1e-300);
        }
    }

    [Theory, MemberData(nameof(WordOrders))]
    public void Format64SignedRoundTrips(WordOrder order)
    {
        foreach (var v in new[] { 0L, 1L, -1L, long.MinValue, long.MaxValue, -1234567890123456789L })
        {
            ValueFormatter.TryParseMultiRegister(
                v.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CellDataType.Signed64, order, out var words).Should().BeTrue();
            var rendered = ValueFormatter.FormatRegister(words, CellDataType.Signed64, order);
            long.Parse(rendered, System.Globalization.CultureInfo.InvariantCulture).Should().Be(v);
        }
    }

    // ─────────── coil parse ─────────────────────────────────────────────

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(" ON ", true)]
    [InlineData("OFF", false)]
    public void ParseCoil_AcceptsStandardTokens(string text, bool expected)
    {
        ValueFormatter.TryParseCoil(text, out var v).Should().BeTrue();
        v.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("yes")]
    [InlineData("2")]
    [InlineData(null)]
    public void ParseCoil_RejectsOther(string? text)
    {
        ValueFormatter.TryParseCoil(text!, out _).Should().BeFalse();
    }

    // ─────────── empty / partial inputs ────────────────────────────────

    [Fact]
    public void Format_EmptyWords_ReturnsEmptyString()
    {
        ValueFormatter.FormatRegister(System.Array.Empty<int>(), CellDataType.Signed32, WordOrder.BigEndian)
            .Should().Be("");
    }

    [Fact]
    public void Format_FewerWordsThanTypeRequires_FallsBackGracefully()
    {
        // 32-bit type with only 1 word — formatter falls back to first word's string
        var s = ValueFormatter.FormatRegister(new[] { 42 }, CellDataType.Signed32, WordOrder.BigEndian);
        s.Should().NotBeNullOrEmpty();
    }
}
