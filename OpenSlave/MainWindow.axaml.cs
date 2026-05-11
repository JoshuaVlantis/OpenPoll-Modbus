using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using EasyModbus;

namespace OpenSlave;

public sealed class CoilCell : INotifyPropertyChanged
{
    private bool _value;
    public int Address { get; init; }
    public bool Value
    {
        get => _value;
        set { if (_value != value) { _value = value; OnPropertyChanged(); OnPropertyChanged(nameof(Display)); } }
    }
    public string Display
    {
        get => _value ? "1" : "0";
        set => Value = (value?.Trim() is "1" or "true" or "on");
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public sealed class RegisterCell : INotifyPropertyChanged
{
    private int _value;
    public int Address { get; init; }
    public int Value
    {
        get => _value;
        set { if (_value != value) { _value = value; OnPropertyChanged(); OnPropertyChanged(nameof(Display)); } }
    }
    public string Display
    {
        get => _value.ToString();
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                Value = v;
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public partial class MainWindow : Window
{
    private const int RegisterCount = 100;
    private ModbusServer? _server;

    public ObservableCollection<CoilCell> Coils { get; } = new();
    public ObservableCollection<CoilCell> Discretes { get; } = new();
    public ObservableCollection<RegisterCell> Holdings { get; } = new();
    public ObservableCollection<RegisterCell> Inputs { get; } = new();
    public ObservableCollection<string> Log { get; } = new();

    private DispatcherTimer _syncTimer = null!;
    private long _reqCount;

    public MainWindow()
    {
        InitializeComponent();

        for (int i = 1; i <= RegisterCount; i++)
        {
            Coils.Add(new CoilCell { Address = i });
            Discretes.Add(new CoilCell { Address = i });
            Holdings.Add(new RegisterCell { Address = i });
            Inputs.Add(new RegisterCell { Address = i });
        }

        CoilsGrid.ItemsSource = Coils;
        DiscreteGrid.ItemsSource = Discretes;
        HoldingGrid.ItemsSource = Holdings;
        InputGrid.ItemsSource = Inputs;
        LogList.ItemsSource = Log;

        _syncTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => SyncFromServer());
    }

    private void OnStart(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_server is not null) return;
            var port = (int)(PortInput.Value ?? 1502);
            _server = new ModbusServer { Port = port };
            _server.NumberOfConnectedClientsChanged += () => Append($"client count = {_server?.NumberOfConnections ?? 0}");
            _server.CoilsChanged += (addr, qty) => Append($"clients changed coils @ {addr} × {qty}");
            _server.HoldingRegistersChanged += (addr, qty) => Append($"clients changed HRs @ {addr} × {qty}");

            // Seed server data from grid
            for (int i = 0; i < RegisterCount; i++)
            {
                _server.coils[i + 1] = Coils[i].Value;
                _server.discreteInputs[i + 1] = Discretes[i].Value;
                _server.holdingRegisters[i + 1] = (short)Holdings[i].Value;
                _server.inputRegisters[i + 1] = (short)Inputs[i].Value;
            }

            _server.Listen();
            _syncTimer.Start();

            SetStatus(true, $"Listening on 0.0.0.0:{port}");
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            Append($"started on port {port}");
        }
        catch (Exception ex)
        {
            SetStatus(false, "Error: " + ex.Message);
        }
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        try
        {
            _syncTimer.Stop();
            _server?.StopListening();
            _server = null;
            SetStatus(false, "Stopped");
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            Append("stopped");
        }
        catch (Exception ex)
        {
            SetStatus(false, "Error: " + ex.Message);
        }
    }

    private void SyncFromServer()
    {
        if (_server is null) return;
        _reqCount++;
        for (int i = 0; i < RegisterCount; i++)
        {
            // Read server back into grid (clients may have modified)
            if (Coils[i].Value != _server.coils[i + 1]) Coils[i].Value = _server.coils[i + 1];
            if (Discretes[i].Value != _server.discreteInputs[i + 1]) Discretes[i].Value = _server.discreteInputs[i + 1];
            if (Holdings[i].Value != _server.holdingRegisters[i + 1]) Holdings[i].Value = _server.holdingRegisters[i + 1];
            if (Inputs[i].Value != _server.inputRegisters[i + 1]) Inputs[i].Value = _server.inputRegisters[i + 1];
        }
        StatsText.Text = $"sync ticks: {_reqCount}  ·  clients: {_server.NumberOfConnections}";
    }

    private void OnCoilEdit(object? sender, DataGridRowEditEndedEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.DataContext is CoilCell c && _server is not null) _server.coils[c.Address] = c.Value;
    }

    private void OnDiscreteEdit(object? sender, DataGridRowEditEndedEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.DataContext is CoilCell c && _server is not null) _server.discreteInputs[c.Address] = c.Value;
    }

    private void OnHoldingEdit(object? sender, DataGridRowEditEndedEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.DataContext is RegisterCell r && _server is not null) _server.holdingRegisters[r.Address] = (short)r.Value;
    }

    private void OnInputEdit(object? sender, DataGridRowEditEndedEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.DataContext is RegisterCell r && _server is not null) _server.inputRegisters[r.Address] = (short)r.Value;
    }

    private void Append(string msg)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {msg}";
        Dispatcher.UIThread.Post(() =>
        {
            Log.Add(line);
            while (Log.Count > 1000) Log.RemoveAt(0);
            if (AutoScrollInput.IsChecked == true && Log.Count > 0)
                LogList.ScrollIntoView(Log[^1]);
        });
    }

    private void OnClearLog(object? sender, RoutedEventArgs e) => Log.Clear();

    private void SetStatus(bool ok, string text)
    {
        StatusDot.Fill = ok ? new SolidColorBrush(Color.Parse("#4ADE80")) : new SolidColorBrush(Color.Parse("#888"));
        StatusText.Text = text;
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            _syncTimer?.Stop();
            _server?.StopListening();
        }
        catch { }
        base.OnClosed(e);
    }
}
