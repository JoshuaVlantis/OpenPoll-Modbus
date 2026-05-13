using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OpenPoll.Models;

namespace OpenPoll.Views;

public partial class FontPickerView : Window
{
    private readonly PollDefinition _definition;

    public FontPickerView() : this(new PollDefinition()) { }

    public FontPickerView(PollDefinition definition)
    {
        _definition = definition;
        InitializeComponent();

        FamilyInput.ItemsSource = SystemFontFamilies();
        FamilyInput.Text = string.IsNullOrEmpty(definition.DisplayFontFamily) ? "" : definition.DisplayFontFamily;
        SizeInput.Value = (decimal)(definition.DisplayFontSize > 0 ? definition.DisplayFontSize : 13);
        FamilyInput.TextChanged += (_, _) => UpdatePreview();
        SizeInput.ValueChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }

    private static IEnumerable<string> SystemFontFamilies()
    {
        // Avalonia 11 exposes the platform's installed font families via FontManager.
        var fm = FontManager.Current;
        var families = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in fm.SystemFonts) families.Add(f.Name);
        return families;
    }

    private void UpdatePreview()
    {
        var family = FamilyInput.Text;
        if (!string.IsNullOrWhiteSpace(family))
        {
            try { PreviewText.FontFamily = new FontFamily(family); }
            catch { /* invalid family — keep current preview font */ }
        }
        if (SizeInput.Value is decimal v) PreviewText.FontSize = (double)v;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        _definition.DisplayFontFamily = FamilyInput.Text ?? "";
        _definition.DisplayFontSize = (double)(SizeInput.Value ?? 13m);
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        FamilyInput.Text = "";
        SizeInput.Value = 13;
    }
}
