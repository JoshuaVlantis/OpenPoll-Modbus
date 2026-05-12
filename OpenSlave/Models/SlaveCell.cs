using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenSlave.Services;

namespace OpenSlave.Models;

/// <summary>
/// One row in a coil/discrete-input table.
/// </summary>
public sealed class CoilCell : INotifyPropertyChanged
{
    private bool _value;

    /// <summary>Wire-protocol address (always 0-based). Display formatting respects the configured base.</summary>
    public int Address { get; init; }
    public int DisplayAddress { get; set; }

    public bool Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnChanged();
            OnChanged(nameof(Display));
        }
    }

    public string Display
    {
        get => _value ? "1" : "0";
        set
        {
            if (ValueFormatter.TryParseCoil(value ?? "", out var parsed))
                Value = parsed;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

/// <summary>
/// One row in a holding/input register table. Carries its own data type
/// so the grid can render and parse multi-register values (32/64-bit) per row.
/// </summary>
public sealed class RegisterCell : INotifyPropertyChanged
{
    private int _rawValue;
    private int[] _rawWords = new int[4];
    private CellDataType _dataType = CellDataType.Unsigned;
    private WordOrder _wordOrder = WordOrder.BigEndian;
    private bool _isEditing;

    /// <summary>Wire-protocol address (always 0-based).</summary>
    public int Address { get; init; }
    public int DisplayAddress { get; set; }

    /// <summary>True while the user is actively editing this row's value cell. The sync timer
    /// uses this to skip overwriting the cell mid-edit.</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set { if (_isEditing != value) { _isEditing = value; OnChanged(); } }
    }

    /// <summary>Raw 16-bit word stored at this address.</summary>
    public int RawValue
    {
        get => _rawValue;
        set
        {
            if (_rawValue == value) return;
            _rawValue = value;
            OnChanged();
            OnChanged(nameof(Display));
        }
    }

    /// <summary>Up to 4 consecutive raw words for multi-register types (this row + neighbours).</summary>
    public int[] RawWords
    {
        get => _rawWords;
        set
        {
            // Avoid spurious PropertyChanged from identical-content reassignments —
            // they were defeating Avalonia's editing UI by re-running the binding mid-edit.
            if (_rawWords.Length == value.Length)
            {
                bool same = true;
                for (int i = 0; i < value.Length; i++) if (_rawWords[i] != value[i]) { same = false; break; }
                if (same) return;
            }
            _rawWords = value;
            OnChanged();
            OnChanged(nameof(Display));
        }
    }

    public CellDataType DataType
    {
        get => _dataType;
        set
        {
            if (_dataType == value) return;
            _dataType = value;
            OnChanged();
            OnChanged(nameof(Display));
            OnChanged(nameof(DataTypeLabel));
        }
    }

    public WordOrder WordOrder
    {
        get => _wordOrder;
        set
        {
            if (_wordOrder == value) return;
            _wordOrder = value;
            OnChanged();
            OnChanged(nameof(Display));
        }
    }

    public string DataTypeLabel => _dataType.Label();

    public string Display
    {
        get
        {
            var wc = _dataType.WordCount();
            if (wc == 1) return ValueFormatter.FormatRegister(_rawValue, _dataType);
            return ValueFormatter.FormatRegister(_rawWords, _dataType, _wordOrder);
        }
        set
        {
            // Without this setter Avalonia's DataGridTextColumn refuses to enter edit mode.
            var wc = _dataType.WordCount();
            if (wc == 1)
            {
                if (ValueFormatter.TryParseRegister(value ?? "", _dataType, out var raw))
                    RawValue = raw;
            }
            else
            {
                if (ValueFormatter.TryParseMultiRegister(value ?? "", _dataType, _wordOrder, out var words))
                {
                    var snapshot = new int[Math.Max(_rawWords.Length, words.Length)];
                    Array.Copy(_rawWords, snapshot, _rawWords.Length);
                    for (int i = 0; i < words.Length; i++) snapshot[i] = words[i];
                    RawWords = snapshot;
                    if (snapshot.Length > 0) RawValue = snapshot[0];
                }
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
