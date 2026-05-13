using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace OpenPoll.Views;

/// <summary>
/// Converts a CSS-style hex string ("#RRGGBB" or "#AARRGGBB") to an <see cref="IBrush"/>.
/// Returns <see cref="AvaloniaProperty.UnsetValue"/> for null/empty so the theme default brush wins.
/// </summary>
public sealed class HexStringToBrushConverter : IValueConverter
{
    public static readonly HexStringToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return Avalonia.AvaloniaProperty.UnsetValue;
        try
        {
            return new ImmutableSolidColorBrush(Color.Parse(s));
        }
        catch
        {
            return Avalonia.AvaloniaProperty.UnsetValue;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
