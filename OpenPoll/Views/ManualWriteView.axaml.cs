using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenPoll.Services;

namespace OpenPoll.Views;

/// <summary>
/// Modbus Poll-style modal write entry. Bound to F5/F6/F7/F8 in the main window — see
/// <see cref="HomeView"/>. The dialog stays generic across the four FCs; the caller chooses
/// the variant via <see cref="WriteFunction"/>.
/// </summary>
public partial class ManualWriteView : Window
{
    public enum WriteFunction { SingleCoil05, SingleRegister06, MultipleCoils15, MultipleRegisters16 }

    private readonly PollDocument _document;
    private readonly WriteFunction _function;

    public ManualWriteView() : this(new PollDocument(new Models.PollDefinition()), WriteFunction.SingleRegister06) { }

    public ManualWriteView(PollDocument document, WriteFunction function, int? prefillAddress = null)
    {
        _document = document;
        _function = function;
        InitializeComponent();

        (HeaderText.Text, HintText.Text, ValueInput.Watermark) = function switch
        {
            WriteFunction.SingleCoil05       => ("FC 05 — Write Single Coil", "Value: 1 or 0 (or true/false / on/off)", "1"),
            WriteFunction.SingleRegister06   => ("FC 06 — Preset Single Register", "Value: integer (16-bit). Hex prefix 0x supported.", "42"),
            WriteFunction.MultipleCoils15    => ("FC 15 — Force Multiple Coils", "Values: comma-separated 1/0 list, e.g. 1,0,1,1", "1,0,1"),
            WriteFunction.MultipleRegisters16 => ("FC 16 — Preset Multiple Registers", "Values: comma-separated integers, e.g. 10,20,30", "10,20,30"),
            _ => ("Write", "", ""),
        };

        if (prefillAddress is int a) AddressInput.Text = a.ToString(CultureInfo.InvariantCulture);
        AddressInput.Focus();
    }

    private async void OnSend(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(AddressInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var address))
        {
            StatusText.Text = "Invalid address";
            return;
        }
        var value = ValueInput.Text ?? "";

        try
        {
            ModbusResult result;
            switch (_function)
            {
                case WriteFunction.SingleCoil05:
                    result = await _document.WriteCoilAsync(address, ParseBool(value));
                    break;
                case WriteFunction.SingleRegister06:
                    result = await _document.WriteRegisterAsync(address, ParseRegister(value));
                    break;
                case WriteFunction.MultipleCoils15:
                {
                    var bools = value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => ParseBool(s.Trim())).ToArray();
                    if (bools.Length == 0) { StatusText.Text = "Provide at least one value"; return; }
                    result = await _document.WriteMultipleCoilsAsync(address, bools);
                    break;
                }
                case WriteFunction.MultipleRegisters16:
                {
                    var ints = value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => ParseRegister(s.Trim())).ToArray();
                    if (ints.Length == 0) { StatusText.Text = "Provide at least one value"; return; }
                    result = await _document.WriteMultipleRegistersAsync(address, ints);
                    break;
                }
                default: return;
            }

            if (result.Success) Close(true);
            else StatusText.Text = result.Error ?? "Write failed";
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private static bool ParseBool(string s) =>
        s.Trim().ToLowerInvariant() is "1" or "true" or "on" or "yes";

    private static int ParseRegister(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt32(s[2..], 16);
        return int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
