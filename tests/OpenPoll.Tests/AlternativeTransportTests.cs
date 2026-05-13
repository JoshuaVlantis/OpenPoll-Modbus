using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using OpenPoll.Models;
using OpenPoll.Services;
using OpenSlave.Services;

namespace OpenPoll.Tests;

/// <summary>
/// End-to-end round-trips for each non-default transport: UDP, RTU-over-TCP, ASCII-over-TCP, TLS.
/// Each test brings up the in-process slave on a free port, runs the OpenPoll master against it
/// in the matching mode, and checks values flow both ways.
/// </summary>
public sealed class AlternativeTransportTests
{
    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static int FreeUdpPort()
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }

    [Fact]
    public void Udp_ReadHoldingRegisters_RoundTrips()
    {
        // UDP listener: pick a port via UdpClient bind+close, then use it (slight TOCTOU window
        // is OK in a single-process unit test).
        using var slave = new ModbusTcpSlave();
        int port = FreeUdpPort();
        slave.StartUdp(port);
        slave.HoldingRegisters[10] = 42;

        using var s = new ModbusSession();
        var def = new PollDefinition { ConnectionMode = ConnectionMode.Udp, IpAddress = "127.0.0.1", ServerPort = port, NodeId = 1, ConnectionTimeoutMs = 2000, ResponseTimeoutMs = 2000 };
        s.Connect(def).Success.Should().BeTrue();
        var r = s.ReadHoldingRegisters(10, 1);
        r.Success.Should().BeTrue(r.Error);
        r.Value!.Single().Should().Be(42);
    }

    [Fact]
    public void RtuOverTcp_WriteThenRead_RoundTrips()
    {
        using var slave = new ModbusTcpSlave();
        int port = FreePort();
        slave.StartRtuOverTcp(port);

        using var s = new ModbusSession();
        var def = new PollDefinition { ConnectionMode = ConnectionMode.RtuOverTcp, IpAddress = "127.0.0.1", ServerPort = port, NodeId = 1, ConnectionTimeoutMs = 2000, ResponseTimeoutMs = 2000 };
        s.Connect(def).Success.Should().BeTrue();
        s.WriteSingleRegister(7, 1234).Success.Should().BeTrue();
        var r = s.ReadHoldingRegisters(7, 1);
        r.Success.Should().BeTrue(r.Error);
        r.Value!.Single().Should().Be(1234);
    }

    [Fact]
    public void AsciiOverTcp_WriteMultipleRegisters_Persisted()
    {
        using var slave = new ModbusTcpSlave();
        int port = FreePort();
        slave.StartAsciiOverTcp(port);

        using var s = new ModbusSession();
        var def = new PollDefinition { ConnectionMode = ConnectionMode.AsciiOverTcp, IpAddress = "127.0.0.1", ServerPort = port, NodeId = 1, ConnectionTimeoutMs = 2000, ResponseTimeoutMs = 2000 };
        s.Connect(def).Success.Should().BeTrue();
        s.WriteMultipleRegisters(20, new[] { 100, 200, 300 }).Success.Should().BeTrue();
        var r = s.ReadHoldingRegisters(20, 3);
        r.Success.Should().BeTrue(r.Error);
        r.Value.Should().Equal(100, 200, 300);
    }

    [Fact]
    public void TcpTls_ReadHoldingRegisters_RoundTrips()
    {
        using var slave = new ModbusTcpSlave();
        int port = FreePort();
        slave.StartTls(port);
        slave.HoldingRegisters[0] = 0xCAFE;

        using var s = new ModbusSession();
        var def = new PollDefinition { ConnectionMode = ConnectionMode.TcpTls, IpAddress = "127.0.0.1", ServerPort = port, NodeId = 1, ConnectionTimeoutMs = 5000, ResponseTimeoutMs = 2000 };
        var connect = s.Connect(def);
        connect.Success.Should().BeTrue(connect.Error);
        var r = s.ReadHoldingRegisters(0, 1);
        r.Success.Should().BeTrue(r.Error);
        r.Value!.Single().Should().Be(0xCAFE);
    }
}
