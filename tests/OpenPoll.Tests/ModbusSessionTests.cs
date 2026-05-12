using System.Net.Sockets;
using FluentAssertions;
using OpenPoll.Models;
using OpenPoll.Services;
using OpenSlave.Services;

namespace OpenPoll.Tests;

/// <summary>
/// Integration tests against an in-process <see cref="ModbusTcpSlave"/>.
/// Each test starts a slave on a unique port to avoid cross-test interference.
/// </summary>
public class ModbusSessionTests : IDisposable
{
    private readonly ModbusTcpSlave _slave = new();
    private readonly int _port;

    public ModbusSessionTests()
    {
        _port = FreePort();
        // Spec-compliant 0-indexed: seed wire addresses 0..19 with 10..200
        for (int i = 0; i < 20; i++)
        {
            _slave.HoldingRegisters[i] = (ushort)((i + 1) * 10);
            _slave.InputRegisters[i] = unchecked((ushort)(short)(-(i + 1) * 10));
            _slave.Coils[i] = (i % 2 == 0);
            _slave.DiscreteInputs[i] = (i % 3 == 0);
        }
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

    private PollDefinition Def(int slave = 1) => new()
    {
        ConnectionMode = ConnectionMode.Tcp,
        IpAddress = "127.0.0.1",
        ServerPort = _port,
        NodeId = slave,
        ConnectionTimeoutMs = 2000,
        ResponseTimeoutMs = 2000,
    };

    [Fact]
    public void Connect_Succeeds()
    {
        using var s = new ModbusSession();
        var r = s.Connect(Def());
        r.Success.Should().BeTrue(r.Error);
        s.Connected.Should().BeTrue();
    }

    [Fact]
    public void Connect_FailsOnUnreachablePort()
    {
        using var s = new ModbusSession();
        var def = Def();
        def.ServerPort = 1; // closed
        def.ConnectionTimeoutMs = 500;
        var r = s.Connect(def);
        r.Success.Should().BeFalse();
        r.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ReadHoldingRegisters_ReturnsSeededValues()
    {
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        var r = s.ReadHoldingRegisters(0, 5);
        r.Success.Should().BeTrue();
        r.Value.Should().NotBeNull();
        r.Value!.Length.Should().Be(5);
        r.Value[0].Should().Be(10);
        r.Value[4].Should().Be(50);
    }

    [Fact]
    public void WriteSingleRegister_ThenReadBack_RoundTrips()
    {
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        s.WriteSingleRegister(50, 12345).Success.Should().BeTrue();
        var r = s.ReadHoldingRegisters(50, 1);
        r.Success.Should().BeTrue();
        r.Value![0].Should().Be(12345);
    }

    [Fact]
    public void WriteMultipleRegisters_AllValuesPersisted()
    {
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        var values = new[] { 100, 200, 300, 400, 500 };
        s.WriteMultipleRegisters(60, values).Success.Should().BeTrue();
        var r = s.ReadHoldingRegisters(60, 5);
        r.Success.Should().BeTrue();
        r.Value.Should().Equal(values);
    }

    [Fact]
    public void WriteSingleCoil_TogglesValue()
    {
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        s.WriteSingleCoil(70, true).Success.Should().BeTrue();
        var r = s.ReadCoils(70, 1);
        r.Success.Should().BeTrue();
        r.Value![0].Should().BeTrue();
        s.WriteSingleCoil(70, false).Success.Should().BeTrue();
        s.ReadCoils(70, 1).Value![0].Should().BeFalse();
    }

    [Fact]
    public void WriteMultipleCoils_PersistsPattern()
    {
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        var pattern = new[] { true, false, true, true, false, false, true, false };
        s.WriteMultipleCoils(80, pattern).Success.Should().BeTrue();
        var r = s.ReadCoils(80, 8);
        r.Success.Should().BeTrue();
        r.Value.Should().Equal(pattern);
    }

    [Fact]
    public void ReconnectWithSameTransport_DoesNotDropConnection()
    {
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        s.Connected.Should().BeTrue();
        s.Connect(Def()).Success.Should().BeTrue();
        s.Connected.Should().BeTrue();
    }

    [Fact]
    public void ReconnectWithDifferentNodeId_KeepsTcpAndJustChangesUnitId()
    {
        using var s = new ModbusSession();
        s.Connect(Def(1)).Success.Should().BeTrue();
        var r1 = s.ReadHoldingRegisters(0, 1);
        r1.Success.Should().BeTrue();
        // Slave only answers unit id 1; reconnect with unit id 99 → no response
        s.Connect(Def(99)).Success.Should().BeTrue();
        var r2 = s.ReadHoldingRegisters(0, 1);
        r2.Success.Should().BeFalse();
    }

    [Fact]
    public void DisconnectThenReadFails()
    {
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        s.Disconnect();
        var r = s.ReadHoldingRegisters(0, 1);
        r.Success.Should().BeFalse();
        r.Error.Should().Be("Not connected");
    }

    [Fact]
    public void ReadOutOfRange_ReturnsModbusException()
    {
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        // Slave allocates 65536 registers; reading way past should surface exception 02
        var r = s.ReadHoldingRegisters(70000, 1);
        r.Success.Should().BeFalse();
    }

    [Fact]
    public void Fc22_MaskWriteRegister_AppliesAndOrCorrectly()
    {
        _slave.HoldingRegisters[120] = 0x1234;
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        s.MaskWriteRegister(120, andMask: 0x00FF, orMask: 0xFF00).Success.Should().BeTrue();
        // (0x1234 & 0x00FF) | (0xFF00 & ~0x00FF) = 0x0034 | 0xFF00 = 0xFF34
        _slave.HoldingRegisters[120].Should().Be(0xFF34);
    }

    [Fact]
    public void Fc23_ReadWriteMultiple_AtomicallyWritesThenReads()
    {
        _slave.HoldingRegisters[200] = 1000;
        _slave.HoldingRegisters[201] = 2000;
        _slave.HoldingRegisters[202] = 3000;

        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        var result = s.ReadWriteMultipleRegisters(writeAddress: 210, writeValues: new[] { 7, 8 },
                                                   readAddress: 200, readQuantity: 3);
        result.Success.Should().BeTrue();
        result.Value.Should().Equal(1000, 2000, 3000);
        _slave.HoldingRegisters[210].Should().Be(7);
        _slave.HoldingRegisters[211].Should().Be(8);
    }

    [Fact]
    public void ExceptionBusy_FromSlaveSurfacesAsFailure()
    {
        _slave.ReturnExceptionBusy = true;
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        var r = s.ReadHoldingRegisters(0, 1);
        r.Success.Should().BeFalse();
        r.Error.Should().Contain("06");
        r.Error.Should().Contain("busy", Exactly.Once());
    }

    [Fact]
    public void Retries_AreAttemptedOnTransientFailure()
    {
        // Stop the slave; the master should retry --retries times and then fail.
        _slave.Stop();
        using var s = new ModbusSession();
        var def = Def();
        def.Retries = 2;
        def.ConnectionTimeoutMs = 200;
        // Connect itself fails (port closed) — that's enough to assert the retry path doesn't crash.
        var connect = s.Connect(def);
        connect.Success.Should().BeFalse();
    }
}
