using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OpenSlave.Models;
using OpenSlave.Services;

namespace OpenSlave;

public partial class MainWindow : Window
{
    private readonly SlaveDocument _document;
    private DispatcherTimer _syncTimer = null!;

    /// <summary>Next default TCP port handed out to a freshly opened slave window. Auto-increments
    /// so two MainWindows in the same process don't collide on 1502.</summary>
    private static int _nextDefaultPort = 1502;

    public ObservableCollection<string> Log { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        var def = new SlaveDefinition
        {
            Port = System.Threading.Interlocked.Increment(ref _nextDefaultPort) - 1,
            SlaveId = 1,
            StartAddress = 0,
            Quantity = 100,
            AddressBase = AddressBase.One,
        };
        _document = new SlaveDocument(def);

        CoilsGrid.ItemsSource = _document.Coils;
        DiscreteGrid.ItemsSource = _document.DiscreteInputs;
        HoldingGrid.ItemsSource = _document.HoldingRegisters;
        InputGrid.ItemsSource = _document.InputRegisters;
        LogList.ItemsSource = Log;

        _document.RequestHandled += ev =>
        {
            FileLogger.Request(ev.FunctionCode, ev.Address, ev.Quantity, ev.Detail);
            Append($"client {FunctionLabel(ev.FunctionCode)} @ {ev.Address} × {ev.Quantity} {ev.Detail}".TrimEnd());
        };
        _document.ConnectedClientsChanged += n =>
        {
            FileLogger.ClientChange(n);
            Append($"client count = {n}");
        };

        SyncDefinitionToInputs();
        _syncTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => OnSyncTick());
    }

    // ─────── Definition ↔ inputs ─────────────────────────────────────

    private void SyncDefinitionToInputs()
    {
        var d = _document.Definition;
        PortInput.Value = d.Port;
        SlaveIdInput.Value = d.SlaveId;
        StartAddressInput.Value = d.StartAddress;
        QuantityInput.Value = d.Quantity;
        AddressBaseInput.SelectedIndex = d.AddressBase == AddressBase.Zero ? 0 : 1;
        IgnoreUnitIdInput.IsChecked = d.IgnoreUnitId;
        ResponseDelayInput.Value = d.ErrorSimulation.ResponseDelayMs;
        SkipResponsesInput.IsChecked = d.ErrorSimulation.SkipResponses;
        ExceptionBusyInput.IsChecked = d.ErrorSimulation.ReturnExceptionBusy;
    }

    private void SyncInputsToDefinition()
    {
        var d = _document.Definition;
        d.Port = (int)(PortInput.Value ?? 1502);
        d.SlaveId = (int)(SlaveIdInput.Value ?? 1);
        d.StartAddress = (int)(StartAddressInput.Value ?? 0);
        d.Quantity = (int)(QuantityInput.Value ?? 100);
        d.AddressBase = AddressBaseInput.SelectedIndex == 0 ? AddressBase.Zero : AddressBase.One;
        d.IgnoreUnitId = IgnoreUnitIdInput.IsChecked == true;
        d.ErrorSimulation.ResponseDelayMs = (int)(ResponseDelayInput.Value ?? 0);
        d.ErrorSimulation.SkipResponses = SkipResponsesInput.IsChecked == true;
        d.ErrorSimulation.ReturnExceptionBusy = ExceptionBusyInput.IsChecked == true;
    }

    private void OnApplyDefinition(object? sender, RoutedEventArgs e)
    {
        SyncInputsToDefinition();
        _document.ApplyDefinitionToSlave();
        _document.RebuildCells();
        Append("definition applied");
    }

    // ─────── Lifecycle ───────────────────────────────────────────────

    private void OnStart(object? sender, RoutedEventArgs e)
    {
        try
        {
            SyncInputsToDefinition();
            _document.Start();
            _syncTimer.Start();
            SetStatus(true, _document.StatusMessage);
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            var msg = $"started on port {_document.Definition.Port} (unit id {_document.Definition.SlaveId})";
            FileLogger.Info(msg);
            Append(msg);
        }
        catch (Exception ex)
        {
            FileLogger.Error("start failed: " + ex);
            SetStatus(false, "Error: " + ex.Message);
        }
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        try
        {
            _syncTimer.Stop();
            _document.Stop();
            SetStatus(false, "Stopped");
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            FileLogger.Info("stopped");
            Append("stopped");
        }
        catch (Exception ex)
        {
            FileLogger.Error("stop failed: " + ex);
            SetStatus(false, "Error: " + ex.Message);
        }
    }

    private void OnSyncTick()
    {
        _document.TickPatterns();
        _document.SyncFromSlave();
        StatsText.Text = $"requests: {_document.RequestCount}  ·  clients: {_document.ConnectedClients}";
    }

    private async void OnOpenPatterns(object? sender, RoutedEventArgs e)
    {
        try
        {
            var view = new Views.PatternsView(_document.Patterns);
            await view.ShowDialog(this);
        }
        catch (Exception ex)
        {
            FileLogger.Error("patterns dialog failed: " + ex);
            SetStatus(_document.Running, "Error: " + ex.Message);
        }
    }

    // ─────── Cell edits ──────────────────────────────────────────────

    private void OnCoilEdit(object? sender, DataGridRowEditEndedEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.DataContext is CoilCell c) _document.WriteCoilFromUi(c.Address, c.Value);
    }

    private void OnDiscreteEdit(object? sender, DataGridRowEditEndedEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.DataContext is CoilCell c) _document.WriteDiscreteFromUi(c.Address, c.Value);
    }

    private void OnRowBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.DataContext is RegisterCell r) r.IsEditing = true;
    }

    private void OnHoldingEdit(object? sender, DataGridRowEditEndedEventArgs e)
    {
        if (e.Row.DataContext is RegisterCell r)
        {
            r.IsEditing = false;
            if (e.EditAction == DataGridEditAction.Commit) WriteRegisterToSlave(r, holding: true);
        }
    }

    private void OnInputEdit(object? sender, DataGridRowEditEndedEventArgs e)
    {
        if (e.Row.DataContext is RegisterCell r)
        {
            r.IsEditing = false;
            if (e.EditAction == DataGridEditAction.Commit) WriteRegisterToSlave(r, holding: false);
        }
    }

    private void WriteRegisterToSlave(RegisterCell r, bool holding)
    {
        var stride = r.DataType.WordCount();
        for (int i = 0; i < stride && i < r.RawWords.Length; i++)
        {
            int addr = r.Address + i;
            ushort value = (ushort)r.RawWords[i];
            if (holding) _document.WriteHoldingFromUi(addr, value);
            else _document.WriteInputFromUi(addr, value);
        }
        if (stride == 1)
        {
            // Single-word path: RawWords may not have been touched if user typed a 16-bit number,
            // so fall back on the parsed RawValue as a safety net.
            ushort value = (ushort)r.RawValue;
            if (holding) _document.WriteHoldingFromUi(r.Address, value);
            else _document.WriteInputFromUi(r.Address, value);
        }
    }

    // ─────── Holding register type/order context menu ────────────────

    private void OnSetType_Holding_Signed(object? s, RoutedEventArgs e)     => SetType(HoldingGrid, CellDataType.Signed);
    private void OnSetType_Holding_Unsigned(object? s, RoutedEventArgs e)   => SetType(HoldingGrid, CellDataType.Unsigned);
    private void OnSetType_Holding_Hex(object? s, RoutedEventArgs e)        => SetType(HoldingGrid, CellDataType.Hex);
    private void OnSetType_Holding_Binary(object? s, RoutedEventArgs e)     => SetType(HoldingGrid, CellDataType.Binary);
    private void OnSetType_Holding_Signed32(object? s, RoutedEventArgs e)   => SetType(HoldingGrid, CellDataType.Signed32);
    private void OnSetType_Holding_Unsigned32(object? s, RoutedEventArgs e) => SetType(HoldingGrid, CellDataType.Unsigned32);
    private void OnSetType_Holding_Hex32(object? s, RoutedEventArgs e)      => SetType(HoldingGrid, CellDataType.Hex32);
    private void OnSetType_Holding_Float(object? s, RoutedEventArgs e)      => SetType(HoldingGrid, CellDataType.Float);
    private void OnSetType_Holding_Signed64(object? s, RoutedEventArgs e)   => SetType(HoldingGrid, CellDataType.Signed64);
    private void OnSetType_Holding_Unsigned64(object? s, RoutedEventArgs e) => SetType(HoldingGrid, CellDataType.Unsigned64);
    private void OnSetType_Holding_Hex64(object? s, RoutedEventArgs e)      => SetType(HoldingGrid, CellDataType.Hex64);
    private void OnSetType_Holding_Double(object? s, RoutedEventArgs e)     => SetType(HoldingGrid, CellDataType.Double);

    private void OnSetOrder_Holding_BE(object? s, RoutedEventArgs e)   => SetOrder(HoldingGrid, WordOrder.BigEndian);
    private void OnSetOrder_Holding_LE(object? s, RoutedEventArgs e)   => SetOrder(HoldingGrid, WordOrder.LittleEndian);
    private void OnSetOrder_Holding_BEBS(object? s, RoutedEventArgs e) => SetOrder(HoldingGrid, WordOrder.BigEndianByteSwap);
    private void OnSetOrder_Holding_LEBS(object? s, RoutedEventArgs e) => SetOrder(HoldingGrid, WordOrder.LittleEndianByteSwap);

    // ─────── Input register type/order context menu ──────────────────

    private void OnSetType_Input_Signed(object? s, RoutedEventArgs e)     => SetType(InputGrid, CellDataType.Signed);
    private void OnSetType_Input_Unsigned(object? s, RoutedEventArgs e)   => SetType(InputGrid, CellDataType.Unsigned);
    private void OnSetType_Input_Hex(object? s, RoutedEventArgs e)        => SetType(InputGrid, CellDataType.Hex);
    private void OnSetType_Input_Binary(object? s, RoutedEventArgs e)     => SetType(InputGrid, CellDataType.Binary);
    private void OnSetType_Input_Signed32(object? s, RoutedEventArgs e)   => SetType(InputGrid, CellDataType.Signed32);
    private void OnSetType_Input_Unsigned32(object? s, RoutedEventArgs e) => SetType(InputGrid, CellDataType.Unsigned32);
    private void OnSetType_Input_Hex32(object? s, RoutedEventArgs e)      => SetType(InputGrid, CellDataType.Hex32);
    private void OnSetType_Input_Float(object? s, RoutedEventArgs e)      => SetType(InputGrid, CellDataType.Float);
    private void OnSetType_Input_Signed64(object? s, RoutedEventArgs e)   => SetType(InputGrid, CellDataType.Signed64);
    private void OnSetType_Input_Unsigned64(object? s, RoutedEventArgs e) => SetType(InputGrid, CellDataType.Unsigned64);
    private void OnSetType_Input_Hex64(object? s, RoutedEventArgs e)      => SetType(InputGrid, CellDataType.Hex64);
    private void OnSetType_Input_Double(object? s, RoutedEventArgs e)     => SetType(InputGrid, CellDataType.Double);

    private void OnSetOrder_Input_BE(object? s, RoutedEventArgs e)   => SetOrder(InputGrid, WordOrder.BigEndian);
    private void OnSetOrder_Input_LE(object? s, RoutedEventArgs e)   => SetOrder(InputGrid, WordOrder.LittleEndian);
    private void OnSetOrder_Input_BEBS(object? s, RoutedEventArgs e) => SetOrder(InputGrid, WordOrder.BigEndianByteSwap);
    private void OnSetOrder_Input_LEBS(object? s, RoutedEventArgs e) => SetOrder(InputGrid, WordOrder.LittleEndianByteSwap);

    private static void SetType(DataGrid grid, CellDataType type)
    {
        if (grid.SelectedItems is null) return;
        foreach (var item in grid.SelectedItems.Cast<RegisterCell>())
            item.DataType = type;
    }

    private static void SetOrder(DataGrid grid, WordOrder order)
    {
        if (grid.SelectedItems is null) return;
        foreach (var item in grid.SelectedItems.Cast<RegisterCell>())
            item.WordOrder = order;
    }

    // ─────── Workspace ───────────────────────────────────────────────

    private async void OnOpenWorkspace(object? sender, RoutedEventArgs e)
    {
        var sp = StorageProvider;
        var picks = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open OpenSlave workspace",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("OpenSlave workspace") { Patterns = new[] { "*.openslave" } },
                FilePickerFileTypes.All,
            }
        });
        if (picks.Count == 0) return;
        var path = picks[0].Path.LocalPath;
        try
        {
            var loaded = WorkspaceFileService.Load(path);
            ApplyLoadedWorkspace(loaded);
            Append($"loaded workspace: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            SetStatus(false, "Load error: " + ex.Message);
        }
    }

    private void ApplyLoadedWorkspace(SlaveDocument loaded)
    {
        // Copy values from the loaded document into our owned one without
        // rebuilding collections from scratch, so XAML bindings stay alive.
        _document.Definition.Name = loaded.Definition.Name;
        _document.Definition.Port = loaded.Definition.Port;
        _document.Definition.SlaveId = loaded.Definition.SlaveId;
        _document.Definition.StartAddress = loaded.Definition.StartAddress;
        _document.Definition.Quantity = loaded.Definition.Quantity;
        _document.Definition.AddressBase = loaded.Definition.AddressBase;
        _document.Definition.IgnoreUnitId = loaded.Definition.IgnoreUnitId;
        _document.Definition.ErrorSimulation.ResponseDelayMs = loaded.Definition.ErrorSimulation.ResponseDelayMs;
        _document.Definition.ErrorSimulation.SkipResponses = loaded.Definition.ErrorSimulation.SkipResponses;
        _document.Definition.ErrorSimulation.ReturnExceptionBusy = loaded.Definition.ErrorSimulation.ReturnExceptionBusy;

        foreach (var c in loaded.Coils.Where(c => c.Value)) _document.WriteCoilFromUi(c.Address, c.Value);
        foreach (var c in loaded.DiscreteInputs.Where(c => c.Value)) _document.WriteDiscreteFromUi(c.Address, c.Value);
        foreach (var r in loaded.HoldingRegisters) _document.WriteHoldingFromUi(r.Address, (ushort)r.RawValue);
        foreach (var r in loaded.InputRegisters) _document.WriteInputFromUi(r.Address, (ushort)r.RawValue);

        _document.RebuildCells();

        // Restore per-cell type/order
        for (int i = 0; i < loaded.HoldingRegisters.Count && i < _document.HoldingRegisters.Count; i++)
        {
            _document.HoldingRegisters[i].DataType = loaded.HoldingRegisters[i].DataType;
            _document.HoldingRegisters[i].WordOrder = loaded.HoldingRegisters[i].WordOrder;
        }
        for (int i = 0; i < loaded.InputRegisters.Count && i < _document.InputRegisters.Count; i++)
        {
            _document.InputRegisters[i].DataType = loaded.InputRegisters[i].DataType;
            _document.InputRegisters[i].WordOrder = loaded.InputRegisters[i].WordOrder;
        }

        SyncDefinitionToInputs();
    }

    private async void OnSaveWorkspace(object? sender, RoutedEventArgs e)
    {
        SyncInputsToDefinition();
        var sp = StorageProvider;
        var pick = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save OpenSlave workspace",
            DefaultExtension = "openslave",
            SuggestedFileName = "slave.openslave",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("OpenSlave workspace") { Patterns = new[] { "*.openslave" } }
            }
        });
        if (pick is null) return;
        try
        {
            WorkspaceFileService.Save(pick.Path.LocalPath, _document);
            Append($"saved workspace: {Path.GetFileName(pick.Path.LocalPath)}");
        }
        catch (Exception ex)
        {
            SetStatus(false, "Save error: " + ex.Message);
        }
    }

    private void OnNewWindow(object? sender, RoutedEventArgs e)
    {
        try
        {
            var fresh = new MainWindow();
            fresh.Show();
            Append($"opened new slave window (default port {fresh._document.Definition.Port})");
        }
        catch (Exception ex)
        {
            FileLogger.Error("new window failed: " + ex);
            SetStatus(_document.Running, "Error: " + ex.Message);
        }
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    private void OnFocusDefinition(object? s, RoutedEventArgs e) => PortInput.Focus();
    private void OnFocusTables(object? s, RoutedEventArgs e) => MainTabs.SelectedIndex = 2;
    private void OnFocusLog(object? s, RoutedEventArgs e) => MainTabs.SelectedIndex = 4;
    private void OnRevealLogFolder(object? s, RoutedEventArgs e)
    {
        FileLogger.RevealLogFolder();
        Append($"log folder: {FileLogger.LogDirectory}");
    }

    // ─────── Log ─────────────────────────────────────────────────────

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

    private async void OnSaveLog(object? sender, RoutedEventArgs e)
    {
        var sp = StorageProvider;
        var pick = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save request log",
            DefaultExtension = "log",
            SuggestedFileName = $"openslave-{DateTime.Now:yyyyMMdd-HHmmss}.log",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text log") { Patterns = new[] { "*.log", "*.txt" } }
            }
        });
        if (pick is null) return;
        try
        {
            await File.WriteAllLinesAsync(pick.Path.LocalPath, Log);
            Append($"log saved: {Path.GetFileName(pick.Path.LocalPath)}");
        }
        catch (Exception ex)
        {
            SetStatus(false, "Log save error: " + ex.Message);
        }
    }

    private void SetStatus(bool ok, string text)
    {
        var (dotKey, surfaceKey, borderKey, fgKey) = ok
            ? ("StatusOkBrush",   "StatusOkSurfaceBrush",   "StatusOkBorderBrush",   "StatusOkBrush")
            : ("StatusIdleBrush", "StatusIdleSurfaceBrush", "StatusIdleBorderBrush", "TextSecondaryBrush");

        if (this.TryFindResource(dotKey, out var dot)     && dot is IBrush b1) StatusDot.Fill = b1;
        if (this.TryFindResource(surfaceKey, out var sur) && sur is IBrush b2) StatusPill.Background = b2;
        if (this.TryFindResource(borderKey, out var bd)   && bd  is IBrush b3) StatusPill.BorderBrush = b3;
        if (this.TryFindResource(fgKey, out var fg)       && fg  is IBrush b4) StatusText.Foreground = b4;
        StatusText.Text = text;
    }

    private static string FunctionLabel(byte fc) => fc switch
    {
        0x01 => "FC01 read coils",
        0x02 => "FC02 read discrete inputs",
        0x03 => "FC03 read holding regs",
        0x04 => "FC04 read input regs",
        0x05 => "FC05 write single coil",
        0x06 => "FC06 write single reg",
        0x0F => "FC15 write multi coils",
        0x10 => "FC16 write multi regs",
        0x16 => "FC22 mask write reg",
        0x17 => "FC23 read/write multi regs",
        _    => $"FC{fc:X2}"
    };

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            _syncTimer?.Stop();
            _document.Dispose();
        }
        catch { }
        base.OnClosed(e);
    }
}
