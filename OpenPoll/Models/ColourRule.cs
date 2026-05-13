using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenPoll.Models;

/// <summary>
/// Conditional display rule: when the row's raw value satisfies <see cref="Comparison"/>,
/// the cell is rendered with <see cref="ColourHex"/> as its foreground.
/// </summary>
public sealed class ColourRule
{
    public ColourComparison Comparison { get; set; } = ColourComparison.Equal;
    public double Value { get; set; }

    /// <summary>Upper bound for <see cref="ColourComparison.Between"/>. Ignored for other comparisons.</summary>
    public double Value2 { get; set; }

    /// <summary>CSS-style "#RRGGBB" or "#AARRGGBB". Defaults to a saturated red.</summary>
    public string ColourHex { get; set; } = "#FF6B6B";

    public bool Matches(double v)
    {
        if (double.IsNaN(v)) return false;
        return Comparison switch
        {
            ColourComparison.Equal       => v == Value,
            ColourComparison.NotEqual    => v != Value,
            ColourComparison.LessThan    => v <  Value,
            ColourComparison.LessOrEqual => v <= Value,
            ColourComparison.GreaterThan => v >  Value,
            ColourComparison.GreaterOrEqual => v >= Value,
            ColourComparison.Between =>
                v >= Math.Min(Value, Value2) && v <= Math.Max(Value, Value2),
            _ => false,
        };
    }

    public ColourRule Clone() => new()
    {
        Comparison = Comparison,
        Value = Value,
        Value2 = Value2,
        ColourHex = ColourHex,
    };

    /// <summary>
    /// First-match-wins: walks the list of rules in order and returns the colour of the first
    /// one whose predicate is satisfied. Returns null if nothing matches.
    /// </summary>
    public static string? FirstMatch(IReadOnlyList<ColourRule>? rules, double value)
    {
        if (rules is null) return null;
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].Matches(value)) return rules[i].ColourHex;
        }
        return null;
    }

    public string Describe()
    {
        var op = Comparison switch
        {
            ColourComparison.Equal => "=",
            ColourComparison.NotEqual => "!=",
            ColourComparison.LessThan => "<",
            ColourComparison.LessOrEqual => "<=",
            ColourComparison.GreaterThan => ">",
            ColourComparison.GreaterOrEqual => ">=",
            ColourComparison.Between => "in",
            _ => "?",
        };
        return Comparison == ColourComparison.Between
            ? $"value {op} [{Value.ToString(CultureInfo.InvariantCulture)}..{Value2.ToString(CultureInfo.InvariantCulture)}]"
            : $"value {op} {Value.ToString(CultureInfo.InvariantCulture)}";
    }
}

public enum ColourComparison
{
    Equal,
    NotEqual,
    LessThan,
    LessOrEqual,
    GreaterThan,
    GreaterOrEqual,
    Between,
}
