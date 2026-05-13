using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenSlave.Models;

namespace OpenSlave.Services;

/// <summary>
/// Runs the set of configured <see cref="Pattern"/>s against a slave's data tables on every tick.
/// One engine instance per slave document; not thread-safe — call <see cref="Tick"/> from a single
/// UI-thread timer (Patterns may also be edited while running, which is OK because <see cref="Apply"/>
/// snapshots them).
/// </summary>
public sealed class PatternEngine
{
    private readonly ModbusTcpSlave _slave;
    private readonly Stopwatch _sw = new();
    private readonly List<RuntimeState> _state = new();

    public PatternEngine(ModbusTcpSlave slave) => _slave = slave;

    /// <summary>Replaces the current pattern set and resets the time origin.</summary>
    public void Apply(IEnumerable<Pattern> patterns)
    {
        _state.Clear();
        var seed = 1;
        foreach (var p in patterns)
        {
            _state.Add(new RuntimeState
            {
                Pattern = p.Clone(),
                Rng = new Random(seed++),
                RwState = p.Offset,
            });
        }
        _sw.Restart();
    }

    public void Tick()
    {
        if (_state.Count == 0) return;
        double t = _sw.Elapsed.TotalMilliseconds;
        for (int i = 0; i < _state.Count; i++)
        {
            var s = _state[i];
            var p = s.Pattern;
            if (p.Address < 0 || p.Address >= ModbusTcpSlave.TableSize) continue;
            double v = Pattern.Evaluate(p.Kind, t, p.Amplitude, p.Offset, p.PeriodMs, ref s.RwState, s.Rng);
            ushort raw = unchecked((ushort)(short)Math.Round(Math.Clamp(v, short.MinValue, short.MaxValue)));
            switch (p.Table)
            {
                case PatternTable.HoldingRegisters: _slave.HoldingRegisters[p.Address] = raw; break;
                case PatternTable.InputRegisters:   _slave.InputRegisters[p.Address]   = raw; break;
            }
        }
    }

    private sealed class RuntimeState
    {
        public Pattern Pattern = default!;
        public Random Rng = default!;
        public double RwState;
    }
}
