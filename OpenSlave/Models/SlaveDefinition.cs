using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenSlave.Models;

/// <summary>
/// Top-level configuration of a single slave: transport, slave ID, table dimensions,
/// addressing convention and error-simulation knobs. The actual cell values live in
/// <see cref="OpenSlave.Services.SlaveDocument"/>.
/// </summary>
public sealed class SlaveDefinition : INotifyPropertyChanged
{
    private string _name = "Slave";
    private int _port = 1502;
    private int _slaveId = 1;
    private int _startAddress;
    private int _quantity = 100;
    private AddressBase _addressBase = AddressBase.One;
    private bool _ignoreUnitId;

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnChanged(); } }
    }

    public int Port
    {
        get => _port;
        set { if (_port != value) { _port = value; OnChanged(); } }
    }

    /// <summary>Modbus Unit Identifier (1..247). When <see cref="IgnoreUnitId"/> is true, all unit IDs are accepted.</summary>
    public int SlaveId
    {
        get => _slaveId;
        set { if (_slaveId != value) { _slaveId = value; OnChanged(); } }
    }

    /// <summary>Wire-protocol start address (always Base 0). Display formatting respects <see cref="AddressBase"/>.</summary>
    public int StartAddress
    {
        get => _startAddress;
        set { if (_startAddress != value) { _startAddress = value; OnChanged(); } }
    }

    public int Quantity
    {
        get => _quantity;
        set { if (_quantity != value) { _quantity = value; OnChanged(); } }
    }

    public AddressBase AddressBase
    {
        get => _addressBase;
        set { if (_addressBase != value) { _addressBase = value; OnChanged(); } }
    }

    public bool IgnoreUnitId
    {
        get => _ignoreUnitId;
        set { if (_ignoreUnitId != value) { _ignoreUnitId = value; OnChanged(); } }
    }

    public ErrorSimulation ErrorSimulation { get; init; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public SlaveDefinition Clone() => new()
    {
        Name = Name,
        Port = Port,
        SlaveId = SlaveId,
        StartAddress = StartAddress,
        Quantity = Quantity,
        AddressBase = AddressBase,
        IgnoreUnitId = IgnoreUnitId,
        ErrorSimulation =
        {
            ResponseDelayMs = ErrorSimulation.ResponseDelayMs,
            SkipResponses = ErrorSimulation.SkipResponses,
            ReturnExceptionBusy = ErrorSimulation.ReturnExceptionBusy,
        }
    };
}
