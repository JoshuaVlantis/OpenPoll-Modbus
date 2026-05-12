using FluentAssertions;
using OpenPoll.Models;

namespace OpenPoll.Tests;

public sealed class RegisterRowDisplayTests
{
    [Fact]
    public void ApplyDisplayTransform_ScalingOffByDefault_ReturnsRaw()
    {
        var row = new RegisterRow { DataType = CellDataType.Unsigned };
        row.ApplyDisplayTransform("100").Should().Be("100");
    }

    [Fact]
    public void ApplyDisplayTransform_ScalingScalesAndOffsets()
    {
        var row = new RegisterRow
        {
            DataType = CellDataType.Unsigned,
            ScalingEnabled = true,
            Scale = 0.1,
            Offset = -5,
            ScalePrecision = 2,
        };
        row.ApplyDisplayTransform("100").Should().Be("5.00");
        row.ApplyDisplayTransform("250").Should().Be("20.00");
    }

    [Fact]
    public void ApplyDisplayTransform_ScalingDisabledForFloat()
    {
        var row = new RegisterRow
        {
            DataType = CellDataType.Float,
            ScalingEnabled = true,
            Scale = 100,
            Offset = 0,
            ScalePrecision = 0,
        };
        // Float types are passed through untouched (already a real number)
        row.ApplyDisplayTransform("3.14").Should().Be("3.14");
    }

    [Fact]
    public void ApplyDisplayTransform_ValueNamesTakePrecedence()
    {
        var row = new RegisterRow
        {
            DataType = CellDataType.Unsigned,
            ValueNames = new() { [0] = "Idle", [1] = "Running", [2] = "Fault" },
        };
        row.ApplyDisplayTransform("0").Should().Be("Idle");
        row.ApplyDisplayTransform("1").Should().Be("Running");
        row.ApplyDisplayTransform("2").Should().Be("Fault");
    }

    [Fact]
    public void ApplyDisplayTransform_ValueNamesFallThroughOnUnmappedValue()
    {
        var row = new RegisterRow
        {
            DataType = CellDataType.Unsigned,
            ValueNames = new() { [0] = "Idle" },
        };
        row.ApplyDisplayTransform("99").Should().Be("99");
    }

    [Fact]
    public void ApplyDisplayTransform_ValueNamesBeforeScaling()
    {
        var row = new RegisterRow
        {
            DataType = CellDataType.Unsigned,
            ScalingEnabled = true,
            Scale = 100,
            ValueNames = new() { [1] = "Special" },
        };
        // Mapped value wins
        row.ApplyDisplayTransform("1").Should().Be("Special");
        // Unmapped value gets scaled
        row.ApplyDisplayTransform("2").Should().Be("200.00");
    }

    [Fact]
    public void ScalePrecision_ClampsToValidRange()
    {
        var row = new RegisterRow();
        row.ScalePrecision = 99;
        row.ScalePrecision.Should().Be(9);
        row.ScalePrecision = -3;
        row.ScalePrecision.Should().Be(0);
    }
}
