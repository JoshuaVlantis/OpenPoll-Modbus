using EasyModbus;
using FluentAssertions;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Tests;

/// <summary>
/// Integration tests against an in-process EasyModbus.ModbusServer.
/// Each test starts a server on a unique port to avoid cross-test interference.
/// </summary>
public class ModbusSessionTests : IDisposable
{
    private static int _portCounter = 11000;
    private readonly ModbusServer _server;
    private readonly int _port;

    public ModbusSessionTests()
    {
        _port = Interlocked.Increment(ref _portCounter);
        _server = new ModbusServer { Port = _port };
        _server.Listen();
        // Seed
        for (int i = 1; i <= 20; i++)
        {
            _server.holdingRegisters[i] = (short)(i * 10);
            _server.inputRegisters[i] = (short)(-i * 10);
            _server.coils[i] = (i % 2 == 0);
            _server.discreteInputs[i] = (i % 3 == 0);
        }
    }

    public void Dispose() => _server.StopListening();

    private PollDefinition Def(int slave = 1) => new()
    {
        ConnectionMode = ConnectionMode.Tcp,
        IpAddress = "127.0.0.1",
        ServerPort = _port,
        NodeId = slave,
        ConnectionTimeoutMs = 2000,
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
        var r = s.ReadHoldingRegisters(0, 5);  // wire addr 0..4 → server array index 1..5
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
        // Connect again with same settings — should short-circuit, stay connected
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
        // Change unit id — slave only answers to id=1, so this should fail at protocol level
        s.Connect(Def(99)).Success.Should().BeTrue();  // TCP connect still ok
        var r2 = s.ReadHoldingRegisters(0, 1);
        r2.Success.Should().BeFalse();  // wrong slave id → no response
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
        // EasyModbus server allocates 65535 registers; reading way past should fail
        var r = s.ReadHoldingRegisters(70000, 1);
        r.Success.Should().BeFalse();
    }
}
