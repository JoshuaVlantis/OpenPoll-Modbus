using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenSlave.Models;

namespace OpenSlave.Views;

public partial class PatternsView : Window
{
    private readonly ObservableCollection<Pattern> _patterns;

    public PatternsView() : this(new ObservableCollection<Pattern>()) { }

    public PatternsView(ObservableCollection<Pattern> patterns)
    {
        _patterns = patterns;
        InitializeComponent();
        Grid.ItemsSource = _patterns;
    }

    private void OnAdd(object? sender, RoutedEventArgs e) => _patterns.Add(new Pattern());

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is Pattern p) _patterns.Remove(p);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
