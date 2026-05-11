using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Views;

public partial class SetupView : Window
{
    public SetupView()
    {
        InitializeComponent();
        var s = SettingsService.Current;
        NodeIdInput.Value = s.NodeId;
        AddressInput.Value = s.Address;
        AmountInput.Value = s.Amount;
        FunctionInput.SelectedIndex = (int)s.Function;
        AddressBaseInput.SelectedIndex = s.DisplayOneIndexed ? 1 : 0;
        WordOrderInput.SelectedIndex = (int)s.WordOrder;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var s = SettingsService.Current;
        s.NodeId = (int)(NodeIdInput.Value ?? 1);
        s.Address = (int)(AddressInput.Value ?? 0);
        s.Amount = (int)(AmountInput.Value ?? 1);
        s.Function = (ModbusFunction)FunctionInput.SelectedIndex;
        s.DisplayOneIndexed = AddressBaseInput.SelectedIndex == 1;
        s.WordOrder = (WordOrder)System.Math.Max(0, WordOrderInput.SelectedIndex);
        SettingsService.Save();
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
