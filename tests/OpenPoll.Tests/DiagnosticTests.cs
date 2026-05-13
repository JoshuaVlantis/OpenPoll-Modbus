using System.Net.Sockets;
using FluentAssertions;
using OpenPoll.Models;
using OpenPoll.Services;
using OpenSlave.Services;

namespace OpenPoll.Tests;

/// <summary>
/// Round-trip tests for FC 08 Diagnostics, FC 11 Get Comm Event Counter, and FC 17 Report Server ID
/// against the in-process slave.
/// </summary>
public sealed class DiagnosticTests : IDisposable
{
    private readonly ModbusTcpSlave _slave = new();
    private readonly int _port;

    public DiagnosticTests()
    {
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        _port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        _slave.Start(_port);
    }

    public void Dispose() => _slave.Dispose();

    private PollDefinition Def() => new()
    {
        ConnectionMode = ConnectionMode.Tcp,
        IpAddress = "127.0.0.1",
        ServerPort = _port,
        NodeId = 1,
        ConnectionTimeoutMs = 2000,
        ResponseTimeoutMs = 2000,
    };

    [Fact]
    public void Fc08_Diagnostic_EchoesQueryData()
    {
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        var r = s.Diagnostic(subFunction: 0x0000, data: 0xCAFE);
        r.Success.Should().BeTrue(r.Error);
        r.Value.Should().Be(0xCAFE);
    }

    [Fact]
    public void Fc11_GetCommEventCounter_IncrementsOnSlaveActivity()
    {
        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();

        var first = s.GetCommEventCounter();
        first.Success.Should().BeTrue(first.Error);
        var initial = first.Value.Count;

        s.ReadHoldingRegisters(0, 1);
        s.ReadHoldingRegisters(0, 1);

        var later = s.GetCommEventCounter();
        later.Success.Should().BeTrue(later.Error);
        // Counter ticks once per non-broadcast request — including these reads and the EC query itself.
        later.Value.Count.Should().BeGreaterThan(initial);
    }

    [Fact]
    public void Fc17_ReportServerId_ReturnsProductNameAndRunStatus()
    {
        _slave.DeviceIdentification.ProductName = "Bench";

        using var s = new ModbusSession();
        s.Connect(Def()).Success.Should().BeTrue();
        var r = s.ReportServerId();
        r.Success.Should().BeTrue(r.Error);
        r.Value.Id.Should().Be("Bench");
        r.Value.RunStatus.Should().BeTrue();
    }
}
