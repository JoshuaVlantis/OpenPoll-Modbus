using FluentAssertions;
using OpenSlave.Services;

namespace OpenSlave.Tests;

/// <summary>
/// Sanity checks for the CRC-16/MODBUS helper. Spec values verified against
/// https://crccalc.com (CRC-16/MODBUS preset, poly 0x8005, init 0xFFFF, refin/refout).
/// </summary>
public sealed class ModbusCrcTests
{
    [Fact]
    public void Compute_MatchesKnownVector_StandardReadHoldingRegister()
    {
        // FC 03 read 1 register from slave 1 @ 0 — hand-derived from the spec polynomial.
        var crc = ModbusCrc.Compute(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01 }, 0, 6);
        crc.Should().Be(0x0A84);
    }

    [Fact]
    public void Compute_RoundTripsThroughWrapAndVerify()
    {
        // Independent sanity: if Compute is wrong but consistent, Verify would still pass — but
        // tampering with a payload byte breaks Verify. That catches the algorithm being broken.
        var frame = ModbusCrc.WrapRtu(0x07, new byte[] { 0x10, 0x00, 0x00, 0x00, 0x02, 0x04, 0xDE, 0xAD, 0xBE, 0xEF });
        ModbusCrc.Verify(frame, 0, frame.Length).Should().BeTrue();
        frame[5] ^= 0x01;
        ModbusCrc.Verify(frame, 0, frame.Length).Should().BeFalse();
    }

    [Fact]
    public void WrapRtu_AppendsLittleEndianCrc()
    {
        var pdu = new byte[] { 0x03, 0x00, 0x00, 0x00, 0x01 };
        var frame = ModbusCrc.WrapRtu(0x01, pdu);
        frame[0].Should().Be(0x01);
        frame[1..6].Should().Equal(pdu);
        // 0x0A84 → low=0x84, high=0x0A
        frame[^2].Should().Be(0x84);
        frame[^1].Should().Be(0x0A);
    }

    [Fact]
    public void Verify_AcceptsValidFrame()
    {
        var pdu = new byte[] { 0x03, 0x00, 0x00, 0x00, 0x01 };
        var frame = ModbusCrc.WrapRtu(0x01, pdu);
        ModbusCrc.Verify(frame, 0, frame.Length).Should().BeTrue();
    }

    [Fact]
    public void Verify_RejectsCorruptFrame()
    {
        var pdu = new byte[] { 0x03, 0x00, 0x00, 0x00, 0x01 };
        var frame = ModbusCrc.WrapRtu(0x01, pdu);
        frame[3] ^= 0xFF;   // flip a payload byte; CRC no longer matches
        ModbusCrc.Verify(frame, 0, frame.Length).Should().BeFalse();
    }
}
