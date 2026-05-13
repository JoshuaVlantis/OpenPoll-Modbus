using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenPoll.Models;

namespace OpenPoll.Views;

public partial class ColourRulesView : Window
{
    private readonly PollDefinition _definition;
    private readonly ObservableCollection<ColourRule> _rules;

    public ColourRulesView() : this(new PollDefinition()) { }

    public ColourRulesView(PollDefinition definition)
    {
        _definition = definition;
        InitializeComponent();
        _rules = new ObservableCollection<ColourRule>(definition.ColourRules.Select(r => r.Clone()));
        Grid.ItemsSource = _rules;
    }

    private void OnAdd(object? sender, RoutedEventArgs e) =>
        _rules.Add(new ColourRule());

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is ColourRule r) _rules.Remove(r);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        _definition.ColourRules = _rules.Select(r => r.Clone()).ToList();
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
