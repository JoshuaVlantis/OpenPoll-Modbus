using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
            dest.Add(new RegisterCell
            {
                Address = addr,
                DisplayAddress = addr + baseShift,
                RawValue = table[addr],
                RawWords = SnapshotWords(table, addr, 4),
            });
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

    public void Stop()
    {
        if (!Running) return;
        _slave.Stop();
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
