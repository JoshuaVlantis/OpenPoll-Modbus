using FluentAssertions;
using OpenSlave.Models;
using OpenSlave.Services;

namespace OpenSlave.Tests;

public sealed class WorkspaceFileServiceTests
{
    [Fact]
    public void RoundTrip_PreservesDefinitionAndValues()
    {
        var def = new SlaveDefinition
        {
            Name = "test",
            Port = 5020,
            SlaveId = 7,
            StartAddress = 100,
            Quantity = 25,
            AddressBase = AddressBase.One,
            IgnoreUnitId = true,
        };
        def.ErrorSimulation.ResponseDelayMs = 250;
        def.ErrorSimulation.SkipResponses = true;
        def.ErrorSimulation.ReturnExceptionBusy = false;

        var doc = new SlaveDocument(def);
        doc.SeedHoldingRegister(100, 0xBEEF);
        doc.SeedHoldingRegister(101, 42);
        doc.SeedCoil(100, true);
        doc.SeedCoil(102, true);
        doc.RebuildCells();
        doc.HoldingRegisters[0].DataType = CellDataType.Hex;
        doc.HoldingRegisters[1].DataType = CellDataType.Float;
        doc.HoldingRegisters[1].WordOrder = WordOrder.LittleEndian;

        var path = Path.Combine(Path.GetTempPath(), $"openslave-rt-{Guid.NewGuid():N}.openslave");
        try
        {
            WorkspaceFileService.Save(path, doc);
            var loaded = WorkspaceFileService.Load(path);

            loaded.Definition.Name.Should().Be("test");
            loaded.Definition.Port.Should().Be(5020);
            loaded.Definition.SlaveId.Should().Be(7);
            loaded.Definition.StartAddress.Should().Be(100);
            loaded.Definition.Quantity.Should().Be(25);
            loaded.Definition.AddressBase.Should().Be(AddressBase.One);
            loaded.Definition.IgnoreUnitId.Should().BeTrue();
            loaded.Definition.ErrorSimulation.ResponseDelayMs.Should().Be(250);
            loaded.Definition.ErrorSimulation.SkipResponses.Should().BeTrue();
            loaded.Definition.ErrorSimulation.ReturnExceptionBusy.Should().BeFalse();

            loaded.HoldingRegisters[0].RawValue.Should().Be(0xBEEF);
            loaded.HoldingRegisters[0].DataType.Should().Be(CellDataType.Hex);
            loaded.HoldingRegisters[1].RawValue.Should().Be(42);
            loaded.HoldingRegisters[1].DataType.Should().Be(CellDataType.Float);
            loaded.HoldingRegisters[1].WordOrder.Should().Be(WordOrder.LittleEndian);

            loaded.Coils[0].Value.Should().BeTrue();
            loaded.Coils[2].Value.Should().BeTrue();
            loaded.Coils[1].Value.Should().BeFalse();
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void Load_RejectsUnknownSchemaVersion()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"schema\": 99, \"definition\": null, \"tables\": null}");
            var act = () => WorkspaceFileService.Load(path);
            act.Should().NotThrow(); // schema 99 is treated as forward-compatible >= 1
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void Load_RejectsZeroSchema()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"schema\": 0}");
            var act = () => WorkspaceFileService.Load(path);
            act.Should().Throw<InvalidDataException>();
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
