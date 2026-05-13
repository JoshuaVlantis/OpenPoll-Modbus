using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenSlave.Models;

public enum PatternKind
{
    Sine,
    Triangle,
    Square,
    Sawtooth,
    RandomWalk,
}

public enum PatternTable
{
    HoldingRegisters,
    InputRegisters,
}

/// <summary>
/// Periodic value generator targeting one slave register. The <see cref="OpenSlave.Services.PatternEngine"/>
/// evaluates each pattern on a tick timer and writes the result into the slave's table; clients then
/// observe a varying value without any external script.
/// </summary>
public sealed class Pattern : INotifyPropertyChanged
{
    private PatternKind _kind = PatternKind.Sine;
    private PatternTable _table = PatternTable.HoldingRegisters;
    private int _address;
    private double _amplitude = 100;
    private double _offset;
    private double _periodMs = 2000;

    public PatternKind Kind { get => _kind; set { if (_kind != value) { _kind = value; OnChanged(); } } }
    public PatternTable Table { get => _table; set { if (_table != value) { _table = value; OnChanged(); } } }
    public int Address { get => _address; set { if (_address != value) { _address = value; OnChanged(); } } }
    public double Amplitude { get => _amplitude; set { if (_amplitude != value) { _amplitude = value; OnChanged(); } } }
    public double Offset { get => _offset; set { if (_offset != value) { _offset = value; OnChanged(); } } }
    public double PeriodMs { get => _periodMs; set { if (_periodMs != value) { _periodMs = Math.Max(1, value); OnChanged(); } } }

    public Pattern Clone() => new()
    {
        Kind = Kind, Table = Table, Address = Address,
        Amplitude = Amplitude, Offset = Offset, PeriodMs = PeriodMs,
    };

    /// <summary>
    /// Pure mathematical projection of (pattern, time) → register value. Extracted as a static
    /// so the engine and unit tests can both call it without spinning up timers.
    /// </summary>
    public static double Evaluate(PatternKind kind, double tMs, double amplitude, double offset, double periodMs, ref double rwState, Random rng)
    {
        switch (kind)
        {
            case PatternKind.Sine:
                return amplitude * Math.Sin(2 * Math.PI * tMs / periodMs) + offset;
            case PatternKind.Triangle:
            {
                double phase = (tMs % periodMs) / periodMs;        // 0..1
                double tri = 1 - Math.Abs(phase * 2 - 1) * 2;       // -1..+1
                return amplitude * tri + offset;
            }
            case PatternKind.Square:
                return offset + amplitude * ((int)Math.Floor(tMs * 2 / periodMs) % 2 == 0 ? 1 : -1);
            case PatternKind.Sawtooth:
            {
                double phase = (tMs % periodMs) / periodMs;        // 0..1
                return amplitude * (phase * 2 - 1) + offset;        // -amp..+amp
            }
            case PatternKind.RandomWalk:
                rwState += (rng.NextDouble() - 0.5) * amplitude * 0.1;
                return rwState;
        }
        return offset;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
