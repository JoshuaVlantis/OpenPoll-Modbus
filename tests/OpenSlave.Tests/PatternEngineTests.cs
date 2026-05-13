using System;
using FluentAssertions;
using OpenSlave.Models;
using OpenSlave.Services;

namespace OpenSlave.Tests;

/// <summary>
/// Mathematical sanity checks for the pattern generators plus an end-to-end test that the
/// <see cref="PatternEngine"/> mutates the slave's register table.
/// </summary>
public sealed class PatternEngineTests
{
    [Fact]
    public void Sine_AtZeroTime_ReturnsOffset()
    {
        double rw = 0;
        var rng = new Random(0);
        var v = Pattern.Evaluate(PatternKind.Sine, tMs: 0, amplitude: 100, offset: 50, periodMs: 1000, ref rw, rng);
        v.Should().BeApproximately(50, 1e-9);
    }

    [Fact]
    public void Sine_AtQuarterPeriod_ReturnsOffsetPlusAmplitude()
    {
        double rw = 0;
        var rng = new Random(0);
        var v = Pattern.Evaluate(PatternKind.Sine, tMs: 250, amplitude: 100, offset: 50, periodMs: 1000, ref rw, rng);
        v.Should().BeApproximately(150, 1e-9);
    }

    [Fact]
    public void Triangle_PeakAtHalfPeriod()
    {
        double rw = 0;
        var rng = new Random(0);
        var peak = Pattern.Evaluate(PatternKind.Triangle, tMs: 500, amplitude: 100, offset: 0, periodMs: 1000, ref rw, rng);
        peak.Should().BeApproximately(100, 1e-6);
    }

    [Fact]
    public void Sawtooth_StartsNegativeEndsPositive()
    {
        double rw = 0; var rng = new Random(0);
        var start = Pattern.Evaluate(PatternKind.Sawtooth, tMs: 0,   amplitude: 100, offset: 0, periodMs: 1000, ref rw, rng);
        var almost = Pattern.Evaluate(PatternKind.Sawtooth, tMs: 999, amplitude: 100, offset: 0, periodMs: 1000, ref rw, rng);
        start.Should().BeApproximately(-100, 1e-9);
        almost.Should().BeApproximately(100 - 0.2, 1e-6);
    }

    [Fact]
    public void Square_AlternatesEveryHalfPeriod()
    {
        double rw = 0; var rng = new Random(0);
        Pattern.Evaluate(PatternKind.Square, tMs: 100, amplitude: 10, offset: 0, periodMs: 1000, ref rw, rng).Should().Be(10);
        Pattern.Evaluate(PatternKind.Square, tMs: 600, amplitude: 10, offset: 0, periodMs: 1000, ref rw, rng).Should().Be(-10);
        Pattern.Evaluate(PatternKind.Square, tMs: 1100, amplitude: 10, offset: 0, periodMs: 1000, ref rw, rng).Should().Be(10);
    }

    [Fact]
    public void Engine_Tick_WritesIntoHoldingRegister()
    {
        var slave = new ModbusTcpSlave();
        var engine = new PatternEngine(slave);
        engine.Apply(new[]
        {
            new Pattern
            {
                Kind = PatternKind.Sine,
                Table = PatternTable.HoldingRegisters,
                Address = 7,
                Amplitude = 50,
                Offset = 100,
                PeriodMs = 1000,
            }
        });

        engine.Tick();
        // Without a real elapsed window the sine starts near phase 0 → value ≈ offset = 100
        var written = (short)slave.HoldingRegisters[7];
        written.Should().BeInRange(90, 110);
    }

    [Fact]
    public void Engine_Tick_NoOpWhenNoPatterns()
    {
        var slave = new ModbusTcpSlave();
        slave.HoldingRegisters[0] = 12345;
        new PatternEngine(slave).Tick();
        slave.HoldingRegisters[0].Should().Be(12345);
    }
}
