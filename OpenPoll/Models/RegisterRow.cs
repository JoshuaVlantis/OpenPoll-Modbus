using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace OpenPoll.Models;

public sealed class RegisterRow : INotifyPropertyChanged
{
    private string _function = "";
    private int _address;
    private int _displayAddress;
    private string _value = "";
    private CellDataType _dataType = CellDataType.Signed;
    private WordOrder _wordOrder = WordOrder.BigEndian;
    private int[] _rawWords = Array.Empty<int>();
    private bool _rawBool;
    private double _scale = 1.0;
    private double _offset;
    private int _scalePrecision = 2;
    private bool _scalingEnabled;
    private Dictionary<long, string>? _valueNames;
    private string? _foregroundHex;
    private bool _isConsumed;

    public string Function
    {
        get => _function;
        set => Set(ref _function, value);
    }

    /// <summary>Wire-level (raw protocol) address. Used for writes.</summary>
    public int Address
    {
        get => _address;
        set => Set(ref _address, value);
    }

    /// <summary>Address as shown in the grid (may be 1-indexed if user opted in).</summary>
    public int DisplayAddress
    {
        get => _displayAddress;
        set => Set(ref _displayAddress, value);
    }

    public string Value
    {
        get => _value;
        set => Set(ref _value, value);
    }

    public CellDataType DataType
    {
        get => _dataType;
        set => Set(ref _dataType, value);
    }

    /// <summary>
    /// Per-row byte/word order for multi-register data types. Seeded from
    /// <see cref="PollDefinition.WordOrder"/> when the row is created; the right-click menu
    /// lets the user override it on a per-row basis (matches Modbus Poll behaviour).
    /// </summary>
    public WordOrder WordOrder
    {
        get => _wordOrder;
        set => Set(ref _wordOrder, value);
    }

    /// <summary>Raw 16-bit words backing this row (1 word for 16-bit types, 2 for 32-bit, 4 for 64-bit).</summary>
    public int[] RawWords
    {
        get => _rawWords;
        set => Set(ref _rawWords, value);
    }

    /// <summary>Convenience: the first word, or 0 if empty. Backwards-compat with 16-bit code paths.</summary>
    public int RawInt
    {
        get => _rawWords.Length > 0 ? _rawWords[0] : 0;
        set => RawWords = new[] { value };
    }

    public bool RawBool
    {
        get => _rawBool;
        set => Set(ref _rawBool, value);
    }

    /// <summary>Linear scale factor: displayed = raw * Scale + Offset.</summary>
    public double Scale
    {
        get => _scale;
        set => Set(ref _scale, value);
    }

    public double Offset
    {
        get => _offset;
        set => Set(ref _offset, value);
    }

    public int ScalePrecision
    {
        get => _scalePrecision;
        set => Set(ref _scalePrecision, Math.Clamp(value, 0, 9));
    }

    /// <summary>When true, Display() applies Scale/Offset to integer raw values.</summary>
    public bool ScalingEnabled
    {
        get => _scalingEnabled;
        set => Set(ref _scalingEnabled, value);
    }

    /// <summary>Optional value→label mapping (e.g. 0→"Idle", 1→"Running"). When set and a key matches, takes precedence over numeric formatting.</summary>
    public Dictionary<long, string>? ValueNames
    {
        get => _valueNames;
        set => Set(ref _valueNames, value);
    }

    /// <summary>
    /// CSS-style hex string driving the VALUE cell's foreground colour, or null for the
    /// theme default. Recomputed each poll tick by <see cref="OpenPoll.Services.PollDocument"/>
    /// from the poll's <see cref="ColourRule"/> list.
    /// </summary>
    public string? ForegroundHex
    {
        get => _foregroundHex;
        set => Set(ref _foregroundHex, value);
    }

    /// <summary>
    /// True when this row's wire register is "consumed" by a multi-word data type on a row above
    /// (e.g. row N is the second word of a Float-32 declared on row N-1). The grid dims these rows
    /// so the relationship is visible — same affordance as Modbus Poll's greying.
    /// </summary>
    public bool IsConsumed
    {
        get => _isConsumed;
        set
        {
            if (_isConsumed == value) return;
            _isConsumed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConsumed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConsumedOpacity)));
        }
    }

    /// <summary>Bindable opacity for the row when consumed (0.35) vs. owning (1.0).</summary>
    public double ConsumedOpacity => _isConsumed ? 0.35 : 1.0;

    /// <summary>
    /// Returns the cell display string. If a value name is mapped to the current raw value, returns the name.
    /// Otherwise applies scaling (when enabled and the type is integer).
    /// </summary>
    public string ApplyDisplayTransform(string formattedRaw)
    {
        if (_valueNames is { Count: > 0 } && long.TryParse(formattedRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asLong)
            && _valueNames.TryGetValue(asLong, out var name))
            return name;

        if (_scalingEnabled && IsIntegerType(_dataType)
            && long.TryParse(formattedRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw))
        {
            var scaled = raw * _scale + _offset;
            return scaled.ToString("F" + _scalePrecision, CultureInfo.InvariantCulture);
        }

        return formattedRaw;
    }

    private static bool IsIntegerType(CellDataType t) => t is
        CellDataType.Signed or CellDataType.Unsigned
        or CellDataType.Signed32 or CellDataType.Unsigned32
        or CellDataType.Signed64 or CellDataType.Unsigned64;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
