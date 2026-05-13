using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using OpenSlave.Models;

namespace OpenSlave.Services;

/// <summary>
/// Owns one configured slave: its definition, the live <see cref="ModbusTcpSlave"/> instance,
/// and observable cell collections bound to the UI grids. UI mutations push through to the slave's
/// data tables; client mutations push back to the UI via the periodic sync tick.
/// </summary>
public sealed class SlaveDocument : INotifyPropertyChanged, IDisposable
{
    private readonly ModbusTcpSlave _slave = new();
    private bool _running;
    private string _statusMessage = "Stopped";
    private long _requestCount;

    public SlaveDefinition Definition { get; }

    /// <summary>Pattern generators driving register values on each tick (sine/triangle/etc).</summary>
    public ObservableCollection<Pattern> Patterns { get; } = new();
    public PatternEngine PatternEngine { get; }

    public ObservableCollection<CoilCell> Coils { get; } = new();
    public ObservableCollection<CoilCell> DiscreteInputs { get; } = new();
    public ObservableCollection<RegisterCell> HoldingRegisters { get; } = new();
    public ObservableCollection<RegisterCell> InputRegisters { get; } = new();

    public bool Running
    {
        get => _running;
        private set { if (_running != value) { _running = value; OnChanged(); } }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage != value) { _statusMessage = value; OnChanged(); } }
    }

    public int ConnectedClients => _slave.ConnectedClients;
    public long RequestCount => _requestCount;

    public event Action<ModbusTcpSlave.RequestEvent>? RequestHandled;
    public event Action<int>? ConnectedClientsChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public SlaveDocument(SlaveDefinition definition)
    {
        Definition = definition;
        PatternEngine = new PatternEngine(_slave);
        Patterns.CollectionChanged += (_, _) => PatternEngine.Apply(Patterns);
        RebuildCells();
        Definition.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SlaveDefinition.StartAddress)
                or nameof(SlaveDefinition.Quantity)
                or nameof(SlaveDefinition.AddressBase))
            {
                RebuildCells();
            }
        };
        _slave.RequestHandled += ev =>
        {
            System.Threading.Interlocked.Increment(ref _requestCount);
            OnChanged(nameof(RequestCount));
            RequestHandled?.Invoke(ev);
        };
        _slave.ConnectedClientsChanged += n =>
        {
            OnChanged(nameof(ConnectedClients));
            ConnectedClientsChanged?.Invoke(n);
        };
    }

    /// <summary>Forward a tick to the pattern engine. Called from the UI sync timer.</summary>
    public void TickPatterns() => PatternEngine.Tick();

    /// <summary>
    /// Rebuild the cell collections to match the configured start/quantity/base.
    /// Existing values at addresses still in range are preserved.
    /// </summary>
    public void RebuildCells()
    {
        Repopulate(Coils, _slave.Coils, isBool: true);
        Repopulate(DiscreteInputs, _slave.DiscreteInputs, isBool: true);
        RepopulateRegisters(HoldingRegisters, _slave.HoldingRegisters);
        RepopulateRegisters(InputRegisters, _slave.InputRegisters);
    }

    private void Repopulate(ObservableCollection<CoilCell> dest, bool[] table, bool isBool)
    {
        var start = Math.Max(0, Definition.StartAddress);
        var qty = Math.Max(0, Definition.Quantity);
        var baseShift = Definition.AddressBase == AddressBase.One ? 1 : 0;

        dest.Clear();
        for (int i = 0; i < qty; i++)
        {
            int addr = start + i;
            if (addr >= ModbusTcpSlave.TableSize) break;
            dest.Add(new CoilCell { Address = addr, DisplayAddress = addr + baseShift, Value = table[addr] });
        }
    }

    private void RepopulateRegisters(ObservableCollection<RegisterCell> dest, ushort[] table)
    {
        var start = Math.Max(0, Definition.StartAddress);
        var qty = Math.Max(0, Definition.Quantity);
        var baseShift = Definition.AddressBase == AddressBase.One ? 1 : 0;

        dest.Clear();
        for (int i = 0; i < qty; i++)
        {
            int addr = start + i;
            if (addr >= ModbusTcpSlave.TableSize) break;
            var cell = new RegisterCell
            {
                Address = addr,
                DisplayAddress = addr + baseShift,
                RawValue = table[addr],
                RawWords = SnapshotWords(table, addr, 4),
            };
            // Mirrors PollDocument: changing a cell's data type re-evaluates which neighbouring
            // cells are "consumed" by a multi-word type, so the grid dims them.
            cell.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RegisterCell.DataType))
                    RecomputeConsumedFlags(dest);
            };
            dest.Add(cell);
        }
        RecomputeConsumedFlags(dest);
    }

    private static void RecomputeConsumedFlags(ObservableCollection<RegisterCell> cells)
    {
        for (int i = 0; i < cells.Count; i++) cells[i].IsConsumed = false;
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].IsConsumed) continue;
            int words = Math.Max(1, cells[i].DataType.WordCount());
            for (int j = 1; j < words && i + j < cells.Count; j++)
                cells[i + j].IsConsumed = true;
        }
    }

    private static int[] SnapshotWords(ushort[] table, int address, int count)
    {
        var w = new int[count];
        for (int i = 0; i < count; i++)
        {
            int a = address + i;
            w[i] = a < table.Length ? table[a] : 0;
        }
        return w;
    }

    public void SeedCoil(int address, bool value)
    {
        if (address < 0 || address >= ModbusTcpSlave.TableSize) return;
        _slave.Coils[address] = value;
    }

    public void SeedDiscrete(int address, bool value)
    {
        if (address < 0 || address >= ModbusTcpSlave.TableSize) return;
        _slave.DiscreteInputs[address] = value;
    }

    public void SeedHoldingRegister(int address, ushort value)
    {
        if (address < 0 || address >= ModbusTcpSlave.TableSize) return;
        _slave.HoldingRegisters[address] = value;
    }

    public void SeedInputRegister(int address, ushort value)
    {
        if (address < 0 || address >= ModbusTcpSlave.TableSize) return;
        _slave.InputRegisters[address] = value;
    }

    public void WriteCoilFromUi(int address, bool value)
    {
        if (address < 0 || address >= ModbusTcpSlave.TableSize) return;
        _slave.Coils[address] = value;
    }

    public void WriteDiscreteFromUi(int address, bool value)
    {
        if (address < 0 || address >= ModbusTcpSlave.TableSize) return;
        _slave.DiscreteInputs[address] = value;
    }

    public void WriteHoldingFromUi(int address, ushort value)
    {
        if (address < 0 || address >= ModbusTcpSlave.TableSize) return;
        _slave.HoldingRegisters[address] = value;
    }

    public void WriteInputFromUi(int address, ushort value)
    {
        if (address < 0 || address >= ModbusTcpSlave.TableSize) return;
        _slave.InputRegisters[address] = value;
    }

    public void Start()
    {
        if (Running) return;
        ApplyDefinitionToSlave();
        _slave.Start(Definition.Port);
        Running = true;
        StatusMessage = $"Listening on 0.0.0.0:{Definition.Port}";
    }

    /// <summary>Open a serial port and serve RTU requests on it (coexists with the TCP listener).</summary>
    public void StartSerial(string portName, int baud, System.IO.Ports.Parity parity, System.IO.Ports.StopBits stopBits)
    {
        ApplyDefinitionToSlave();
        _slave.StartSerial(portName, baud, parity, stopBits);
        var note = $"+ serial {portName}@{baud} {parity}/{stopBits}";
        StatusMessage = string.IsNullOrEmpty(StatusMessage) || StatusMessage == "Stopped"
            ? note.TrimStart('+', ' ')
            : StatusMessage + " " + note;
    }

    public void StopSerial() => _slave.StopSerial();

    public void StartUdp(int port) => _slave.StartUdp(port);
    public void StopUdp() => _slave.StopUdp();

    public void StartRtuOverTcp(int port) => _slave.StartRtuOverTcp(port);
    public void StopRtuOverTcp() => _slave.StopRtuOverTcp();

    public void StartAsciiOverTcp(int port) => _slave.StartAsciiOverTcp(port);
    public void StopAsciiOverTcp() => _slave.StopAsciiOverTcp();

    public void StartTls(int port, System.Security.Cryptography.X509Certificates.X509Certificate2? cert = null) => _slave.StartTls(port, cert);
    public void StopTls() => _slave.StopTls();

    public void StartAsciiOverSerial(string portName, int baud, System.IO.Ports.Parity parity, System.IO.Ports.StopBits stopBits) =>
        _slave.StartAsciiOverSerial(portName, baud, parity, stopBits);
    public void StopAsciiOverSerial() => _slave.StopAsciiOverSerial();

    public void StartRtuOverUdp(int port) => _slave.StartRtuOverUdp(port);
    public void StopRtuOverUdp() => _slave.StopRtuOverUdp();

    public void StartAsciiOverUdp(int port) => _slave.StartAsciiOverUdp(port);
    public void StopAsciiOverUdp() => _slave.StopAsciiOverUdp();

    public void Stop()
    {
        if (!Running) return;
        _slave.Stop();
        _slave.StopSerial();
        _slave.StopUdp();
        _slave.StopRtuOverTcp();
        _slave.StopAsciiOverTcp();
        _slave.StopTls();
        _slave.StopAsciiOverSerial();
        _slave.StopRtuOverUdp();
        _slave.StopAsciiOverUdp();
        Running = false;
        StatusMessage = "Stopped";
    }

    public void ApplyDefinitionToSlave()
    {
        _slave.SlaveId = (byte)Math.Clamp(Definition.SlaveId, 0, 255);
        _slave.IgnoreUnitId = Definition.IgnoreUnitId;
        _slave.ResponseDelayMs = Math.Max(0, Definition.ErrorSimulation.ResponseDelayMs);
        _slave.SkipResponses = Definition.ErrorSimulation.SkipResponses;
        _slave.ReturnExceptionBusy = Definition.ErrorSimulation.ReturnExceptionBusy;
    }

    /// <summary>Sync grid cells with current slave-side values (clients may have written).
    /// Cells flagged <see cref="RegisterCell.IsEditing"/> are skipped so we don't yank typed
    /// input out from under the user.</summary>
    public void SyncFromSlave()
    {
        for (int i = 0; i < Coils.Count; i++)
        {
            var c = Coils[i];
            var v = _slave.Coils[c.Address];
            if (c.Value != v) c.Value = v;
        }
        for (int i = 0; i < DiscreteInputs.Count; i++)
        {
            var c = DiscreteInputs[i];
            var v = _slave.DiscreteInputs[c.Address];
            if (c.Value != v) c.Value = v;
        }
        for (int i = 0; i < HoldingRegisters.Count; i++)
        {
            var r = HoldingRegisters[i];
            if (r.IsEditing) continue;
            var v = _slave.HoldingRegisters[r.Address];
            if (r.RawValue != v) r.RawValue = v;
            r.RawWords = SnapshotWords(_slave.HoldingRegisters, r.Address, 4);
        }
        for (int i = 0; i < InputRegisters.Count; i++)
        {
            var r = InputRegisters[i];
            if (r.IsEditing) continue;
            var v = _slave.InputRegisters[r.Address];
            if (r.RawValue != v) r.RawValue = v;
            r.RawWords = SnapshotWords(_slave.InputRegisters, r.Address, 4);
        }
    }

    public void Dispose()
    {
        try { _slave.Dispose(); } catch { }
    }

    private void OnChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
