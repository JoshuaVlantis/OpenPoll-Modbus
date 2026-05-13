using FluentAssertions;
using OpenPoll.Views;

namespace OpenPoll.Tests;

public sealed class TestCenterHexTests
{
    [Theory]
    [InlineData("03 00 00 00 01", new byte[] { 0x03, 0x00, 0x00, 0x00, 0x01 })]
    [InlineData("03,00,00,00,01", new byte[] { 0x03, 0x00, 0x00, 0x00, 0x01 })]
    [InlineData("0x0300000001",   new byte[] { 0x03, 0x00, 0x00, 0x00, 0x01 })]
    [InlineData("AB-CD-EF",       new byte[] { 0xAB, 0xCD, 0xEF })]
    [InlineData("",               new byte[0])]
    public void ParseHexBytes_AcceptsCommonSeparators(string raw, byte[] expected) =>
        TestCenterView.ParseHexBytes(raw).Should().Equal(expected);

    [Fact]
    public void ParseHexBytes_RejectsOddNumberOfDigits()
    {
        var act = () => TestCenterView.ParseHexBytes("123");
        act.Should().Throw<System.FormatException>();
    }
}
