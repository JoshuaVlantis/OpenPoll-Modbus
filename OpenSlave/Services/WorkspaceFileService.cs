using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSlave.Models;

namespace OpenSlave.Services;

/// <summary>
/// Persists a SlaveDocument to disk as JSON (`.openslave`). The format is
/// intentionally human-readable so users can author scenarios in a text editor.
/// </summary>
public static class WorkspaceFileService
{
    public const string Extension = ".openslave";
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Save(string path, SlaveDocument document)
    {
        var snapshot = WorkspaceFile.From(document);
        var json = JsonSerializer.Serialize(snapshot, Options);
        File.WriteAllText(path, json);
    }

    public static SlaveDocument Load(string path)
    {
        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<WorkspaceFile>(json, Options)
                   ?? throw new InvalidDataException("Workspace file is empty.");
        if (file.Schema is not (>= 1))
            throw new InvalidDataException($"Unsupported workspace schema: {file.Schema}");
        return file.ToDocument();
    }

    private sealed class WorkspaceFile
    {
        public int Schema { get; set; } = CurrentSchemaVersion;
        public string? Generator { get; set; }
        public string? CreatedAt { get; set; }
        public DefinitionDto? Definition { get; set; }
        public TablesDto? Tables { get; set; }
        public List<PatternDto>? Patterns { get; set; }

        public static WorkspaceFile From(SlaveDocument doc) => new()
        {
            Schema = CurrentSchemaVersion,
            Generator = "OpenSlave",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            Definition = DefinitionDto.From(doc.Definition),
            Tables = TablesDto.From(doc),
            Patterns = doc.Patterns.Select(PatternDto.From).ToList(),
        };

        public SlaveDocument ToDocument()
        {
            var def = (Definition ?? new DefinitionDto()).ToModel();
            var doc = new SlaveDocument(def);
            Tables?.ApplyTo(doc);
            if (Patterns is { Count: > 0 })
            {
                foreach (var p in Patterns) doc.Patterns.Add(p.ToModel());
            }
            return doc;
        }
    }

    private sealed class PatternDto
    {
        public PatternKind Kind { get; set; } = PatternKind.Sine;
        public PatternTable Table { get; set; } = PatternTable.HoldingRegisters;
        public int Address { get; set; }
        public double Amplitude { get; set; } = 100;
        public double Offset { get; set; }
        public double PeriodMs { get; set; } = 2000;

        public static PatternDto From(Pattern p) => new()
        {
            Kind = p.Kind, Table = p.Table, Address = p.Address,
            Amplitude = p.Amplitude, Offset = p.Offset, PeriodMs = p.PeriodMs,
        };

        public Pattern ToModel() => new()
        {
            Kind = Kind, Table = Table, Address = Address,
            Amplitude = Amplitude, Offset = Offset, PeriodMs = PeriodMs,
        };
    }

    private sealed class DefinitionDto
    {
        public string Name { get; set; } = "Slave";
        public int Port { get; set; } = 1502;
        public int SlaveId { get; set; } = 1;
        public int StartAddress { get; set; }
        public int Quantity { get; set; } = 100;
        public AddressBase AddressBase { get; set; } = AddressBase.One;
        public bool IgnoreUnitId { get; set; }
        public ErrorSimulationDto ErrorSimulation { get; set; } = new();

        public static DefinitionDto From(SlaveDefinition d) => new()
        {
            Name = d.Name,
            Port = d.Port,
            SlaveId = d.SlaveId,
            StartAddress = d.StartAddress,
            Quantity = d.Quantity,
            AddressBase = d.AddressBase,
            IgnoreUnitId = d.IgnoreUnitId,
            ErrorSimulation = new ErrorSimulationDto
            {
                ResponseDelayMs = d.ErrorSimulation.ResponseDelayMs,
                SkipResponses = d.ErrorSimulation.SkipResponses,
                ReturnExceptionBusy = d.ErrorSimulation.ReturnExceptionBusy,
            }
        };

        public SlaveDefinition ToModel()
        {
            var d = new SlaveDefinition
            {
                Name = Name,
                Port = Port,
                SlaveId = SlaveId,
                StartAddress = StartAddress,
                Quantity = Quantity,
                AddressBase = AddressBase,
                IgnoreUnitId = IgnoreUnitId,
            };
            d.ErrorSimulation.ResponseDelayMs = ErrorSimulation.ResponseDelayMs;
            d.ErrorSimulation.SkipResponses = ErrorSimulation.SkipResponses;
            d.ErrorSimulation.ReturnExceptionBusy = ErrorSimulation.ReturnExceptionBusy;
            return d;
        }
    }

    private sealed class ErrorSimulationDto
    {
        public int ResponseDelayMs { get; set; }
        public bool SkipResponses { get; set; }
        public bool ReturnExceptionBusy { get; set; }
    }

    private sealed class TablesDto
    {
        public List<CoilDto> Coils { get; set; } = new();
        public List<CoilDto> DiscreteInputs { get; set; } = new();
        public List<RegisterDto> HoldingRegisters { get; set; } = new();
        public List<RegisterDto> InputRegisters { get; set; } = new();

        public static TablesDto From(SlaveDocument d) => new()
        {
            Coils = d.Coils.Where(c => c.Value).Select(c => new CoilDto { Address = c.Address, Value = true }).ToList(),
            DiscreteInputs = d.DiscreteInputs.Where(c => c.Value).Select(c => new CoilDto { Address = c.Address, Value = true }).ToList(),
            HoldingRegisters = d.HoldingRegisters.Select(r => new RegisterDto
            {
                Address = r.Address,
                Value = r.RawValue,
                DataType = r.DataType,
                WordOrder = r.WordOrder,
            }).Where(r => r.Value != 0 || r.DataType != CellDataType.Unsigned || r.WordOrder != WordOrder.BigEndian).ToList(),
            InputRegisters = d.InputRegisters.Select(r => new RegisterDto
            {
                Address = r.Address,
                Value = r.RawValue,
                DataType = r.DataType,
                WordOrder = r.WordOrder,
            }).Where(r => r.Value != 0 || r.DataType != CellDataType.Unsigned || r.WordOrder != WordOrder.BigEndian).ToList(),
        };

        public void ApplyTo(SlaveDocument d)
        {
            foreach (var c in Coils) d.SeedCoil(c.Address, c.Value);
            foreach (var c in DiscreteInputs) d.SeedDiscrete(c.Address, c.Value);
            foreach (var r in HoldingRegisters) d.SeedHoldingRegister(r.Address, (ushort)r.Value);
            foreach (var r in InputRegisters) d.SeedInputRegister(r.Address, (ushort)r.Value);

            d.RebuildCells();

            // Restore per-cell type/order on rebuild.
            void Apply(List<RegisterDto> src, ObservableCollection<RegisterCell> dest)
            {
                foreach (var dto in src)
                {
                    var cell = dest.FirstOrDefault(x => x.Address == dto.Address);
                    if (cell is null) continue;
                    cell.DataType = dto.DataType;
                    cell.WordOrder = dto.WordOrder;
                }
            }
            Apply(HoldingRegisters, d.HoldingRegisters);
            Apply(InputRegisters, d.InputRegisters);
        }
    }

    private sealed class CoilDto
    {
        public int Address { get; set; }
        public bool Value { get; set; }
    }

    private sealed class RegisterDto
    {
        public int Address { get; set; }
        public int Value { get; set; }
        public CellDataType DataType { get; set; } = CellDataType.Unsigned;
        public WordOrder WordOrder { get; set; } = WordOrder.BigEndian;
    }
}
