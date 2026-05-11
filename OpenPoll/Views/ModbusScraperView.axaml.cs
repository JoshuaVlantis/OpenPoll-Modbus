using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Views;

public partial class ModbusScraperView : Window
{
    private CancellationTokenSource? _scanCts;
    private Task? _scanTask;

    // Buffered output: scan threads enqueue lines, a UI-thread timer drains
    // them periodically. Without this, ResultsView.Text += line per line saturates
    // the dispatcher and Stop clicks can't get processed during long scans.
    private readonly ConcurrentQueue<string> _pendingLines = new();
    private readonly StringBuilder _resultsBuilder = new();
    private readonly DispatcherTimer _flushTimer;

    private const int Mode_Coils = 0;
    private const int Mode_DiscreteInputs = 1;
    private const int Mode_HoldingRegisters = 2;
    private const int Mode_InputRegisters = 3;
    private const int Mode_SlaveIdScan = 4;
    private const int Mode_IpScan = 5;

    private static readonly string DefaultLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenPoll", "log.txt");

    public ModbusScraperView()
    {
        InitializeComponent();
        Load();

        _flushTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(100),
            DispatcherPriority.Background,
            (_, _) => FlushPendingLines());
        _flushTimer.Start();
    }

    private void Load()
    {
        var s = SettingsService.Current;
        IpInput.Text = s.IpAddress;
        PortInput.Value = s.ServerPort;
        TimeoutInput.Value = s.ConnectionTimeoutMs;
        SlaveIdInput.Value = s.NodeId;
        StartInput.Value = s.Address;
        AmountInput.Value = Math.Max(1, s.Amount);

        ModeInput.SelectedIndex = (int)s.Function switch
        {
            (int)ModbusFunction.Coils => Mode_Coils,
            (int)ModbusFunction.DiscreteInputs => Mode_DiscreteInputs,
            (int)ModbusFunction.HoldingRegisters => Mode_HoldingRegisters,
            (int)ModbusFunction.InputRegisters => Mode_InputRegisters,
            _ => Mode_HoldingRegisters
        };

        LogPathHint.Text = "→ " + DefaultLogPath;
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        var mode = ModeInput.SelectedIndex;
        switch (mode)
        {
            case Mode_Coils:
            case Mode_DiscreteInputs:
            case Mode_HoldingRegisters:
            case Mode_InputRegisters:
                IpLabel.Text = "IP address";
                StartLabel.Text = "Starting register";
                AmountLabel.Text = "Amount";
                SlaveIdRow.IsVisible = true;
                AmountRow.IsVisible = true;
                ExtremesInput.IsVisible = true;
                ExtremesInput.Content = "Show extremes (0 / −32768)";
                break;

            case Mode_SlaveIdScan:
                IpLabel.Text = "IP address";
                StartLabel.Text = "Starting ID";
                AmountLabel.Text = "Ending ID";
                SlaveIdRow.IsVisible = false;
                AmountRow.IsVisible = true;
                ExtremesInput.IsVisible = true;
                ExtremesInput.Content = "Break on first ID found";
                break;

            case Mode_IpScan:
                IpLabel.Text = "IP base (e.g. 192.168.1.0)";
                StartLabel.Text = "(unused)";
                AmountLabel.Text = "(unused)";
                SlaveIdRow.IsVisible = false;
                AmountRow.IsVisible = false;
                ExtremesInput.IsVisible = false;
                break;
        }
    }

    // ────────── start / stop ──────────

    private async void OnStart(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_scanTask is not null && !_scanTask.IsCompleted) return;

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            ProgressView.Value = 0;
            StatusText.Text = "Scanning…";
            while (_pendingLines.TryDequeue(out _)) { }
            _resultsBuilder.Clear();
            ResultsView.Text = "";

            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;
            var mode = ModeInput.SelectedIndex;

            var ip = (IpInput.Text ?? "").Trim();
            var port = (int)(PortInput.Value ?? 502);
            var timeout = (int)(TimeoutInput.Value ?? 1000);
            var slave = (int)(SlaveIdInput.Value ?? 1);
            var start = (int)(StartInput.Value ?? 0);
            var amount = (int)(AmountInput.Value ?? 1);
            var extremes = ExtremesInput.IsChecked == true;

            _scanTask = Task.Run(async () =>
            {
                try
                {
                    switch (mode)
                    {
                        case Mode_Coils:
                        case Mode_DiscreteInputs:
                        case Mode_HoldingRegisters:
                        case Mode_InputRegisters:
                            await ScanRegistersAsync(ip, port, timeout, slave, mode, start, amount, extremes, token);
                            break;
                        case Mode_SlaveIdScan:
                            await ScanIdsAsync(ip, port, timeout, start, amount, breakOnFirst: extremes, token);
                            break;
                        case Mode_IpScan:
                            await ScanIpsAsync(ip, port, timeout, token);
                            break;
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    PostLine($"[error] {ex.Message}");
                }
                finally
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        StartButton.IsEnabled = true;
                        StopButton.IsEnabled = false;
                        ProgressView.Value = 0;
                        StatusText.Text = token.IsCancellationRequested ? "Stopped." : "Done.";
                    });
                }
            });

            await _scanTask;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
    }

    protected override void OnClosed(EventArgs e)
    {
        _scanCts?.Cancel();
        base.OnClosed(e);
    }

    // ────────── scans ──────────

    private Task ScanRegistersAsync(
        string ip, int port, int timeoutMs, int slaveId,
        int mode, int start, int amount, bool showExtremes,
        CancellationToken ct)
    {
        var settings = new PollDefinition
        {
            ConnectionMode = ConnectionMode.Tcp,
            IpAddress = ip,
            ServerPort = port,
            ConnectionTimeoutMs = timeoutMs,
            NodeId = slaveId
        };

        using var session = new ModbusSession();
        var connect = session.Connect(settings);
        if (!connect.Success)
        {
            PostLine($"[error] {connect.Error}");
            return Task.CompletedTask;
        }

        const int batchSize = 100;
        var pos = 0;
        var current = batchSize;

        for (int i = 0; i < amount && !ct.IsCancellationRequested;)
        {
            var size = Math.Min(current, amount - i);
            var addr = start + i;

            var (ok, message) = ReadBatch(session, mode, addr, size, out var rendered);
            if (ok)
            {
                foreach (var line in rendered!)
                {
                    if (showExtremes || !LooksExtreme(line))
                        PostLine(line);
                }
                i += size;
                current = batchSize;
                pos = i;
                PostProgress((int)((double)pos / amount * 100));
            }
            else if (message == "starting-address-invalid")
            {
                if (current <= 1) { i += 1; pos = i; continue; }
                current = Math.Max(1, current - 10);
            }
            else
            {
                PostLine($"[error] {message}");
                break;
            }
        }
        return Task.CompletedTask;
    }

    private static (bool ok, string? message) ReadBatch(
        ModbusSession session, int mode, int addr, int size, out string[]? rendered)
    {
        rendered = null;

        switch (mode)
        {
            case Mode_Coils:
            {
                var r = session.ReadCoils(addr, size);
                if (!r.Success) return (false, MapErr(r.Error));
                rendered = Render(addr, r.Value!);
                return (true, null);
            }
            case Mode_DiscreteInputs:
            {
                var r = session.ReadDiscreteInputs(addr, size);
                if (!r.Success) return (false, MapErr(r.Error));
                rendered = Render(addr, r.Value!);
                return (true, null);
            }
            case Mode_HoldingRegisters:
            {
                var r = session.ReadHoldingRegisters(addr, size);
                if (!r.Success) return (false, MapErr(r.Error));
                rendered = Render(addr, r.Value!);
                return (true, null);
            }
            case Mode_InputRegisters:
            {
                var r = session.ReadInputRegisters(addr, size);
                if (!r.Success) return (false, MapErr(r.Error));
                rendered = Render(addr, r.Value!);
                return (true, null);
            }
        }
        return (false, "unknown mode");
    }

    private static string MapErr(string? error) =>
        error == "Illegal data address" ? "starting-address-invalid" : (error ?? "");

    private static string[] Render(int startAddr, bool[] values)
    {
        var rows = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
            rows[i] = $"{startAddr + i,6}  :  {(values[i] ? 1 : 0)}";
        return rows;
    }

    private static string[] Render(int startAddr, int[] values)
    {
        var rows = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
            rows[i] = $"{startAddr + i,6}  :  {values[i]}";
        return rows;
    }

    private static bool LooksExtreme(string line) =>
        line.EndsWith(":  0") || line.EndsWith(":  -32768");

    private Task ScanIdsAsync(
        string ip, int port, int timeoutMs,
        int idStart, int idEnd, bool breakOnFirst,
        CancellationToken ct)
    {
        var settings = new PollDefinition
        {
            ConnectionMode = ConnectionMode.Tcp,
            IpAddress = ip,
            ServerPort = port,
            ConnectionTimeoutMs = timeoutMs,
            NodeId = Math.Max(1, idStart)
        };

        using var session = new ModbusSession();
        var connect = session.Connect(settings);
        if (!connect.Success)
        {
            PostLine($"[error] {connect.Error}");
            return Task.CompletedTask;
        }

        var lo = Math.Max(1, idStart);
        var hi = Math.Max(lo, idEnd);

        for (int id = lo; id <= hi && !ct.IsCancellationRequested; id++)
        {
            settings.NodeId = id;
            session.Connect(settings); // reuses existing connection, just updates UnitIdentifier

            var probe = session.ReadHoldingRegisters(0, 10);
            var found = probe.Success || probe.Error == "Illegal data address";
            if (found)
            {
                PostLine($"Node ID: {id}");
                if (breakOnFirst) break;
            }

            PostProgress((int)((double)(id - lo + 1) / Math.Max(1, hi - lo + 1) * 100));
        }
        return Task.CompletedTask;
    }

    private async Task ScanIpsAsync(
        string ipBase, int port, int timeoutMs, CancellationToken ct)
    {
        var dot = ipBase.LastIndexOf('.');
        if (dot < 0)
        {
            PostLine("[error] IP base must contain dots, e.g. 192.168.1.0");
            return;
        }
        var prefix = ipBase[..(dot + 1)];

        for (int last = 1; last < 255 && !ct.IsCancellationRequested; last++)
        {
            var ip = prefix + last;
            var settings = new PollDefinition
            {
                ConnectionMode = ConnectionMode.Tcp,
                IpAddress = ip,
                ServerPort = port,
                ConnectionTimeoutMs = timeoutMs
            };

            using var session = new ModbusSession();
            var result = session.Connect(settings);
            if (result.Success)
                PostLine($"IP responding: {ip}");

            PostProgress((int)((double)last / 254 * 100));
            await Task.Yield();
        }
    }

    // ────────── output ──────────

    private void PostLine(string line) => _pendingLines.Enqueue(line);

    private void FlushPendingLines()
    {
        if (_pendingLines.IsEmpty) return;

        var batch = new StringBuilder();
        while (_pendingLines.TryDequeue(out var line))
            batch.Append(line).Append('\n');

        _resultsBuilder.Append(batch);
        ResultsView.Text = _resultsBuilder.ToString();
    }

    private void PostProgress(int percent)
    {
        var p = Math.Clamp(percent, 0, 100);
        Dispatcher.UIThread.Post(() => ProgressView.Value = p);
    }

    // ────────── log save ──────────

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        while (_pendingLines.TryDequeue(out _)) { }
        _resultsBuilder.Clear();
        ResultsView.Text = "";
    }

    private async void OnSaveLog(object? sender, RoutedEventArgs e)
    {
        try
        {
            var content = ResultsView.Text ?? "";

            var picker = StorageProvider;
            if (picker is null) return;

            Avalonia.Platform.Storage.IStorageFolder? startLocation = null;
            try
            {
                var dir = Path.GetDirectoryName(DefaultLogPath) ?? Environment.CurrentDirectory;
                startLocation = await picker.TryGetFolderFromPathAsync(new Uri("file://" + dir));
            }
            catch
            {
                // picker will fall back to its default location
            }

            var file = await picker.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = "log.txt",
                DefaultExtension = "txt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Text") { Patterns = new[] { "*.txt" } }
                },
                SuggestedStartLocation = startLocation
            });

            if (file is null) return;

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(content);
            StatusText.Text = "Saved.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Save failed: " + ex.Message;
        }
    }
}
