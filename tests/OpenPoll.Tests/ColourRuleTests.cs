using FluentAssertions;
using OpenPoll.Models;

namespace OpenPoll.Tests;

/// <summary>
/// Pure-evaluator tests for <see cref="ColourRule"/>: comparison operators, first-match-wins
/// semantics, edge cases (NaN, Between with swapped bounds, empty list).
/// </summary>
public sealed class ColourRuleTests
{
    [Theory]
    [InlineData(ColourComparison.Equal, 5.0, 5.0, true)]
    [InlineData(ColourComparison.Equal, 5.0, 4.9, false)]
    [InlineData(ColourComparison.NotEqual, 5.0, 4.0, true)]
    [InlineData(ColourComparison.NotEqual, 5.0, 5.0, false)]
    [InlineData(ColourComparison.LessThan, 10.0, 9.99, true)]
    [InlineData(ColourComparison.LessThan, 10.0, 10.0, false)]
    [InlineData(ColourComparison.LessOrEqual, 10.0, 10.0, true)]
    [InlineData(ColourComparison.GreaterThan, 10.0, 10.01, true)]
    [InlineData(ColourComparison.GreaterThan, 10.0, 10.0, false)]
    [InlineData(ColourComparison.GreaterOrEqual, 10.0, 10.0, true)]
    public void Matches_HandlesSingleValueOperators(ColourComparison op, double threshold, double input, bool expected)
    {
        var rule = new ColourRule { Comparison = op, Value = threshold };
        rule.Matches(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.0, 100.0, -1.0, false)]
    [InlineData(0.0, 100.0, 0.0, true)]
    [InlineData(0.0, 100.0, 50.0, true)]
    [InlineData(0.0, 100.0, 100.0, true)]
    [InlineData(0.0, 100.0, 100.01, false)]
    [InlineData(100.0, 0.0, 50.0, true)]    // swapped bounds still work
    public void Matches_BetweenInclusive(double lo, double hi, double v, bool expected)
    {
        var rule = new ColourRule { Comparison = ColourComparison.Between, Value = lo, Value2 = hi };
        rule.Matches(v).Should().Be(expected);
    }

    [Fact]
    public void Matches_NaN_NeverMatches()
    {
        new ColourRule { Comparison = ColourComparison.Equal, Value = 0 }
            .Matches(double.NaN).Should().BeFalse();
        new ColourRule { Comparison = ColourComparison.Between, Value = 0, Value2 = 100 }
            .Matches(double.NaN).Should().BeFalse();
    }

    [Fact]
    public void FirstMatch_ReturnsColourOfFirstSatisfiedRule()
    {
        var rules = new[]
        {
            new ColourRule { Comparison = ColourComparison.LessThan,    Value = 0,    ColourHex = "#blue" },
            new ColourRule { Comparison = ColourComparison.GreaterThan, Value = 1000, ColourHex = "#red"  },
            new ColourRule { Comparison = ColourComparison.Between,     Value = 0, Value2 = 1000, ColourHex = "#green" },
        };

        ColourRule.FirstMatch(rules, -5).Should().Be("#blue");
        ColourRule.FirstMatch(rules, 5000).Should().Be("#red");
        ColourRule.FirstMatch(rules, 500).Should().Be("#green");
    }

    [Fact]
    public void FirstMatch_ReturnsNull_WhenNothingMatchesOrRulesEmpty()
    {
        ColourRule.FirstMatch(null, 0).Should().BeNull();
        ColourRule.FirstMatch(System.Array.Empty<ColourRule>(), 0).Should().BeNull();

        var rules = new[]
        {
            new ColourRule { Comparison = ColourComparison.GreaterThan, Value = 100 },
        };
        ColourRule.FirstMatch(rules, 50).Should().BeNull();
    }

    [Fact]
    public void PollDefinition_Clone_DeepCopiesColourRules()
    {
        var def = new PollDefinition();
        def.ColourRules.Add(new ColourRule { Comparison = ColourComparison.GreaterThan, Value = 10, ColourHex = "#aaa" });
        var copy = def.Clone();
        copy.ColourRules.Should().HaveCount(1);
        copy.ColourRules[0].ColourHex = "#bbb";
        // Mutating the copy must not affect the original — proves it's a deep copy.
        def.ColourRules[0].ColourHex.Should().Be("#aaa");
    }
}
