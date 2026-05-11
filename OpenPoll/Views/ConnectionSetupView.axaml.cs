using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Views;

public partial class ConnectionSetupView : Window
{
    private static readonly int[] BaudRates = { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };

    public ConnectionSetupView()
    {
        InitializeComponent();
        Load();
    }

    private void Load()
    {
        var s = SettingsService.Current;

        ConnectionModeInput.SelectedIndex = (int)s.ConnectionMode;
        ApplyConnectionModeVisibility();

        IpInput.Text = s.IpAddress;
        PortInput.Value = s.ServerPort;
        ConnectionTimeoutInput.Value = s.ConnectionTimeoutMs;

        RefreshPorts();
        if (!string.IsNullOrEmpty(s.SerialPortName))
        {
            var idx = SerialPortInput.Items.IndexOf(s.SerialPortName);
            if (idx >= 0) SerialPortInput.SelectedIndex = idx;
        }

        var baudIdx = Array.IndexOf(BaudRates, s.BaudRate);
        BaudRateInput.SelectedIndex = baudIdx >= 0 ? baudIdx : 3;

        ParityInput.SelectedIndex = (int)s.Parity;
        StopBitsInput.SelectedIndex = (int)s.StopBits;

        PollingRateInput.Value = s.PollingRateMs;
    }

    private void OnConnectionModeChanged(object? sender, SelectionChangedEventArgs e) =>
        ApplyConnectionModeVisibility();

    private void ApplyConnectionModeVisibility()
    {
        var serial = ConnectionModeInput.SelectedIndex == 1;
        TcpPanel.IsVisible = !serial;
        SerialPanel.IsVisible = serial;
    }

    private void OnRefreshPorts(object? sender, RoutedEventArgs e) => RefreshPorts();

    private void RefreshPorts()
    {
        var ports = EnumerateSerialPorts();
        var current = SerialPortInput.SelectedItem as string;

        SerialPortInput.ItemsSource = ports;

        if (current is not null && ports.Contains(current))
            SerialPortInput.SelectedItem = current;
        else if (ports.Count > 0)
            SerialPortInput.SelectedIndex = 0;
    }

    private static List<string> EnumerateSerialPorts()
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);

        try
        {
            foreach (var name in SerialPort.GetPortNames())
                set.Add(name);
        }
        catch { }

        if (OperatingSystem.IsLinux())
        {
            foreach (var prefix in new[] { "ttyUSB", "ttyACM", "ttyS" })
            {
                try
                {
                    foreach (var path in Directory.EnumerateFiles("/dev", prefix + "*"))
                        set.Add(path);
                }
                catch { }
            }
        }

        return set.ToList();
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var s = SettingsService.Current;

        s.ConnectionMode = ConnectionModeInput.SelectedIndex == 1
            ? ConnectionMode.Serial : ConnectionMode.Tcp;

        s.IpAddress = (IpInput.Text ?? "").Trim();
        s.ServerPort = (int)(PortInput.Value ?? 502);
        s.ConnectionTimeoutMs = (int)(ConnectionTimeoutInput.Value ?? 1000);

        s.SerialPortName = SerialPortInput.SelectedItem as string ?? "";
        var baudIdx = Math.Max(0, BaudRateInput.SelectedIndex);
        s.BaudRate = BaudRates[Math.Min(baudIdx, BaudRates.Length - 1)];
        s.Parity = (Parity)Math.Max(0, ParityInput.SelectedIndex);
        s.StopBits = (StopBits)Math.Max(0, StopBitsInput.SelectedIndex);

        s.PollingRateMs = (int)(PollingRateInput.Value ?? 1000);

        SettingsService.Save();
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
