using FluentAssertions;
using OpenPoll.Models;

namespace OpenPoll.Tests;

public class PollDefinitionTests
{
    [Fact]
    public void Clone_ProducesDeepCopyOfSimpleFields()
    {
        var a = new PollDefinition
        {
            Name = "test",
            IpAddress = "10.1.1.1",
            ServerPort = 503,
            NodeId = 7,
            Address = 100,
            Amount = 25,
            Function = ModbusFunction.InputRegisters,
            WordOrder = WordOrder.LittleEndian,
            DisplayOneIndexed = true,
        };
        var b = a.Clone();
        b.Should().NotBeSameAs(a);
        b.Name.Should().Be("test");
        b.IpAddress.Should().Be("10.1.1.1");
        b.ServerPort.Should().Be(503);
        b.NodeId.Should().Be(7);
        b.Function.Should().Be(ModbusFunction.InputRegisters);
        b.WordOrder.Should().Be(WordOrder.LittleEndian);
        b.DisplayOneIndexed.Should().BeTrue();
    }

    [Fact]
    public void Clone_DoesNotShareState()
    {
        var a = new PollDefinition { IpAddress = "1.2.3.4" };
        var b = a.Clone();
        a.IpAddress = "5.6.7.8";
        b.IpAddress.Should().Be("1.2.3.4");
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        var d = new PollDefinition();
        d.ConnectionMode.Should().Be(ConnectionMode.Tcp);
        d.IpAddress.Should().Be("127.0.0.1");
        d.ServerPort.Should().Be(502);
        d.NodeId.Should().Be(1);
        d.Address.Should().Be(0);
        d.Amount.Should().Be(10);
        d.Function.Should().Be(ModbusFunction.HoldingRegisters);
        d.WordOrder.Should().Be(WordOrder.BigEndian);
        d.DisplayOneIndexed.Should().BeFalse();
    }
}
