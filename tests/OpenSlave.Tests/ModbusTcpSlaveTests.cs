using System.Net.Sockets;
using FluentAssertions;
using OpenSlave.Services;

namespace OpenSlave.Tests;

/// <summary>
/// Byte-level checks against the custom Modbus TCP slave. We hand-craft MBAP frames
/// so the assertions are independent of any client library and exercise the actual wire format.
/// </summary>
public sealed class ModbusTcpSlaveTests : IDisposable
{
    private readonly ModbusTcpSlave _slave = new();
    private readonly int _port;

    public ModbusTcpSlaveTests()
    {
        _port = FreePort();
        _slave.Start(_port);
    }

    public void Dispose() => _slave.Dispose();

    private static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private byte[] Send(byte[] frame)
    {
        using var client = new TcpClient();
        client.Connect("127.0.0.1", _port);
        var stream = client.GetStream();
        stream.Write(frame);

        // Read MBAP header + PDU
        var header = new byte[7];
        ReadExact(stream, header, 0, 7);
        int length = (header[4] << 8) | header[5]; // includes unit id
        var rest = new byte[length - 1];
        if (rest.Length > 0) ReadExact(stream, rest, 0, rest.Length);
        var full = new byte[7 + rest.Length];
        Buffer.BlockCopy(header, 0, full, 0, 7);
        Buffer.BlockCopy(rest, 0, full, 7, rest.Length);
        return full;
    }

    private static void ReadExact(NetworkStream s, byte[] buf, int off, int len)
    {
        int total = 0;
        while (total < len)
        {
            var n = s.Read(buf, off + total, len - total);
            if (n == 0) throw new EndOfStreamException();
            total += n;
        }
    }

    [Fact]
    public void Fc03_ReadHoldingRegisters_ReturnsSeededValues()
    {
        _slave.HoldingRegisters[0] = 111;
        _slave.HoldingRegisters[1] = 222;
        _slave.HoldingRegisters[2] = 333;

        // MBAP: txn=0001, proto=0000, len=0006, unit=01
        // PDU: fc=03, addr=0000, qty=0003
        var frame = new byte[] { 0, 1, 0, 0, 0, 6, 1, 3, 0, 0, 0, 3 };
        var resp = Send(frame);

        // Expected: MBAP txn=0001, proto=0000, len=0009, unit=01, fc=03, bytecount=06, then 3×u16 BE
        resp.Should().Equal(new byte[]
        {
            0, 1, 0, 0, 0, 9, 1, 3, 6,
            0, 111, 0, 222, 1, 77,  // 333 = 0x014D
        });
    }

    [Fact]
    public void Fc03_OutOfRange_ReturnsException02()
    {
        // Read 1 register starting at 0xFFFF + 0 → addr+qty > tablesize
        var frame = new byte[] { 0, 2, 0, 0, 0, 6, 1, 3, 0xFF, 0xFF, 0, 2 };
        var resp = Send(frame);

        // Exception response: fc | 0x80, exception code 02
        resp[7].Should().Be(0x83);
        resp[8].Should().Be(0x02);
    }

    [Fact]
    public void Fc06_WriteSingleRegister_UpdatesTableAndEchoes()
    {
        var frame = new byte[] { 0, 1, 0, 0, 0, 6, 1, 6, 0, 10, 1, 2 };
        var resp = Send(frame);

        _slave.HoldingRegisters[10].Should().Be(0x0102);
        resp[7..].Should().Equal(new byte[] { 6, 0, 10, 1, 2 });
    }

    [Fact]
    public void Fc22_MaskWriteRegister_AppliesAndOrCorrectly()
    {
        _slave.HoldingRegisters[5] = 0x1234;

        // PDU: fc=16, addr=0005, AND=00FF, OR=FF00
        var frame = new byte[] { 0, 1, 0, 0, 0, 8, 1, 0x16, 0, 5, 0, 0xFF, 0xFF, 0 };
        var resp = Send(frame);

        // Result = (0x1234 & 0x00FF) | (0xFF00 & ~0x00FF) = 0x34 | 0xFF00 = 0xFF34
        _slave.HoldingRegisters[5].Should().Be(0xFF34);
        // Echo of request (after MBAP)
        resp[7..].Should().Equal(new byte[] { 0x16, 0, 5, 0, 0xFF, 0xFF, 0 });
    }

    [Fact]
    public void Fc23_ReadWriteMultiple_AtomicallyWritesThenReads()
    {
        _slave.HoldingRegisters[0] = 100;
        _slave.HoldingRegisters[1] = 200;
        _slave.HoldingRegisters[2] = 300;

        // Read 3 from addr 0, write 2 (val=99,98) at addr 10
        // PDU: fc=17, readAddr=0000, readQty=0003, writeAddr=000A, writeQty=0002, byteCount=04, vals=00 63 00 62
        var frame = new byte[] { 0, 1, 0, 0, 0, 0x0F, 1, 0x17, 0, 0, 0, 3, 0, 10, 0, 2, 4, 0, 99, 0, 62 }; // 62 not 98 — fixing
        // Correct byte for 98 = 0x62
        // 99 = 0x63
        // values bytes: 00 63 00 62
        frame[17] = 0; frame[18] = 99; frame[19] = 0; frame[20] = 98;
        var resp = Send(frame);

        _slave.HoldingRegisters[10].Should().Be(99);
        _slave.HoldingRegisters[11].Should().Be(98);

        // Response: MBAP + fc=17, byteCount=06, 3×u16 (100, 200, 300)
        resp[7..].Should().Equal(new byte[]
        {
            0x17, 6,
            0, 100, 0, 200, 1, 0x2C,
        });
    }

    [Fact]
    public void IgnoreUnitId_AcceptsAnyUnitId()
    {
        _slave.HoldingRegisters[0] = 42;
        _slave.SlaveId = 7;
        _slave.IgnoreUnitId = true;

        // Request with unit=99 — should still respond
        var frame = new byte[] { 0, 1, 0, 0, 0, 6, 99, 3, 0, 0, 0, 1 };
        var resp = Send(frame);
        resp[7..].Should().Equal(new byte[] { 3, 2, 0, 42 });
    }

    [Fact]
    public void ExceptionBusy_ForcesException06ForAllRequests()
    {
        _slave.ReturnExceptionBusy = true;

        var frame = new byte[] { 0, 1, 0, 0, 0, 6, 1, 3, 0, 0, 0, 1 };
        var resp = Send(frame);
        resp[7].Should().Be(0x83);
        resp[8].Should().Be(0x06);
    }

    [Fact]
    public void Fc43_BasicStream_ReturnsMandatoryObjects()
    {
        _slave.DeviceIdentification.VendorName = "OpenPoll Project";
        _slave.DeviceIdentification.ProductCode = "OpenSlave";
        _slave.DeviceIdentification.MajorMinorRevision = "2.1.0";

        // PDU: 0x2B 0x0E code=0x01 (basic) objectId=0x00 → len=4 + unit = 5
        var frame = new byte[] { 0, 1, 0, 0, 0, 5, 1, 0x2B, 0x0E, 0x01, 0x00 };
        var resp = Send(frame);

        // Response PDU starts at offset 7. 0x2B 0x0E 0x01 conformity moreFollows nextId numObjs ...
        resp[7].Should().Be(0x2B);
        resp[8].Should().Be(0x0E);
        resp[9].Should().Be(0x01);          // code echoed
        resp[10].Should().Be(0x81);         // basic + individual access support
        resp[11].Should().Be(0x00);         // more follows = no
        resp[13].Should().Be(0x03);         // 3 mandatory objects
        // First object id=0 length=16 "OpenPoll Project"
        resp[14].Should().Be(0x00);
        resp[15].Should().Be((byte)"OpenPoll Project".Length);
    }

    [Fact]
    public void Fc43_Individual_ReturnsRequestedObjectOnly()
    {
        _slave.DeviceIdentification.ModelName = "Bench";

        // PDU: 0x2B 0x0E code=0x04 (individual) objectId=0x05
        var frame = new byte[] { 0, 1, 0, 0, 0, 5, 1, 0x2B, 0x0E, 0x04, 0x05 };
        var resp = Send(frame);
        resp[7].Should().Be(0x2B);
        resp[8].Should().Be(0x0E);
        resp[9].Should().Be(0x04);
        resp[13].Should().Be(0x01);   // exactly one object
        resp[14].Should().Be(0x05);   // ModelName
        resp[15].Should().Be(0x05);   // length 5
        System.Text.Encoding.UTF8.GetString(resp, 16, 5).Should().Be("Bench");
    }

    [Fact]
    public void Fc43_UnknownObjectIndividual_ReturnsException02()
    {
        _slave.DeviceIdentification.ModelName = "";
        // Request individual object 0x05 with model name cleared → exception 02
        var frame = new byte[] { 0, 1, 0, 0, 0, 5, 1, 0x2B, 0x0E, 0x04, 0x05 };
        var resp = Send(frame);
        resp[7].Should().Be(0xAB);   // 0x2B | 0x80
        resp[8].Should().Be(0x02);
    }
}
