using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenPoll.Models;

public sealed class RegisterRow : INotifyPropertyChanged
{
    private string _function = "";
    private int _address;
    private int _displayAddress;
    private string _value = "";
    private CellDataType _dataType = CellDataType.Signed;
    private int[] _rawWords = Array.Empty<int>();
    private bool _rawBool;

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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
