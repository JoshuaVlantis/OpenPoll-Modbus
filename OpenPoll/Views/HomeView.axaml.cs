using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Views;

internal enum StatusKind { Idle, Ok, Err }

public partial class HomeView : Window
{
    private readonly Workspace _workspace = new();
    private PollDocument _document = null!;
    private HttpApiHost? _httpApi;

    public HomeView()
    {
        InitializeComponent();

        // Seed with a single doc using whatever's in settings.json
        var initial = SettingsService.Current;
        if (string.IsNullOrWhiteSpace(initial.Name)) initial.Name = "Poll 1";
        var doc = _workspace.AddExisting(new PollDocument(initial));
        _workspace.PropertyChanged += OnWorkspacePropertyChanged;
        TabStrip.ItemsSource = _workspace.Documents;
        BindDocument(doc);
        ApplyFunctionUiState(_document.Definition.Function);
        UpdateDataSubtitle(_document.Definition);
        RefreshRecentMenu();

        // Modbus-Poll-style write keybindings: F5 single coil, F6 single reg, F7 multi coil, F8 multi reg.
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F5), Command = new RelayCommand(() => OpenManualWrite(ManualWriteView.WriteFunction.SingleCoil05)) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F6), Command = new RelayCommand(() => OpenManualWrite(ManualWriteView.WriteFunction.SingleRegister06)) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F7), Command = new RelayCommand(() => OpenManualWrite(ManualWriteView.WriteFunction.MultipleCoils15)) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F8), Command = new RelayCommand(() => OpenManualWrite(ManualWriteView.WriteFunction.MultipleRegisters16)) });
    }

    private async void OpenManualWrite(ManualWriteView.WriteFunction fn)
    {
        try
        {
            int? prefill = (DataGrid.SelectedItem as RegisterRow)?.Address;
            var ok = await new ManualWriteView(_document, fn, prefill).ShowDialog<bool>(this);
            if (ok) ApplyStatusVisuals(StatusKind.Ok, "Write OK");
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private void OnManualWrite05(object? s, RoutedEventArgs e) => OpenManualWrite(ManualWriteView.WriteFunction.SingleCoil05);
    private void OnManualWrite06(object? s, RoutedEventArgs e) => OpenManualWrite(ManualWriteView.WriteFunction.SingleRegister06);
    private void OnManualWrite15(object? s, RoutedEventArgs e) => OpenManualWrite(ManualWriteView.WriteFunction.MultipleCoils15);
    private void OnManualWrite16(object? s, RoutedEventArgs e) => OpenManualWrite(ManualWriteView.WriteFunction.MultipleRegisters16);

    /// <summary>Minimal <see cref="System.Windows.Input.ICommand"/> wrapper for keybinding lambdas.</summary>
    private sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public RelayCommand(Action action) => _action = action;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
        // CanExecute never changes for these key bindings — the event exists only for interface conformance.
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    private void RefreshRecentMenu()
    {
        if (RecentMenu is null) return;
        RecentMenu.Items.Clear();
        var recent = RecentFilesService.Load();
        if (recent.Count == 0)
        {
            RecentMenu.Items.Add(new MenuItem { Header = "(none)", IsEnabled = false });
            return;
        }
        foreach (var path in recent)
        {
            var item = new MenuItem
            {
                Header = System.IO.Path.GetFileName(path),
                Tag = path,
            };
            ToolTip.SetTip(item, path);
            item.Click += async (_, _) =>
            {
                try
                {
                    var loaded = await WorkspaceFileService.LoadFromPathAsync(path);
                    if (loaded is null) return;
                    foreach (var d in _workspace.Documents.ToList())
                    {
                        try { await d.StopAsync(); } catch { }
                        _workspace.Close(d);
                    }
                    foreach (var def in loaded) _workspace.AddNew(def);
                    if (_workspace.Documents.Count > 0)
                        _workspace.Active = _workspace.Documents[0];
                    RefreshRecentMenu();
                }
                catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
            };
            RecentMenu.Items.Add(item);
        }
        RecentMenu.Items.Add(new Separator());
        var clear = new MenuItem { Header = "Clear list" };
        clear.Click += (_, _) => { RecentFilesService.Clear(); RefreshRecentMenu(); };
        RecentMenu.Items.Add(clear);
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Workspace.Active)) return;
        if (_workspace.Active is null) return;
        BindDocument(_workspace.Active);
        ApplyFunctionUiState(_document.Definition.Function);
        UpdateDataSubtitle(_document.Definition);
    }

    private void BindDocument(PollDocument doc)
    {
        if (_document is not null)
            _document.PropertyChanged -= OnDocumentPropertyChanged;

        _document = doc;
        _document.PropertyChanged += OnDocumentPropertyChanged;
        DataGrid.ItemsSource = _document.Rows;
        SettingsService.SetCurrent(_document.Definition);
        SyncFromDocument();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PollDocument.Status):
            case nameof(PollDocument.StatusMessage):
                SyncStatus();
                break;
            case nameof(PollDocument.PollCount):
                PollCountText.Text = _document.PollCount.ToString();
                break;
        }
    }

    private void SyncFromDocument()
    {
        SyncStatus();
        PollCountText.Text = _document.PollCount.ToString();
    }

    private void SyncStatus()
    {
        var kind = _document.Status switch
        {
            PollStatus.Connected => StatusKind.Ok,
            PollStatus.Error => StatusKind.Err,
            _ => StatusKind.Idle
        };
        ApplyStatusVisuals(kind, _document.StatusMessage);

        var running = _document.IsRunning;
        ConnectButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
    }

    // ────────── tab handling ──────────

    private void OnTabPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border b && b.Tag is PollDocument doc)
            _workspace.Active = doc;
    }

    private void OnNewTab(object? sender, RoutedEventArgs e)
    {
        var template = _workspace.Active?.Definition ?? new PollDefinition();
        var doc = _workspace.AddNew(template);
        _workspace.Active = doc;
    }

    private void OnDuplicateTab(object? sender, RoutedEventArgs e)
    {
        if (_workspace.Active is null) return;
        var dup = _workspace.AddNew(_workspace.Active.Definition);
        dup.Definition.Name = _workspace.Active.Definition.Name + " (copy)";
        _workspace.Active = dup;
    }

    private void OnCloseActive(object? sender, RoutedEventArgs e)
    {
        if (_workspace.Active is { } a) CloseDocument(a);
    }

    private void OnCloseTab(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PollDocument doc)
            CloseDocument(doc);
    }

    private async void CloseDocument(PollDocument doc)
    {
        try { await doc.StopAsync(); } catch { }
        if (_workspace.Documents.Count <= 1)
        {
            // Don't allow closing the last tab — replace it with a fresh blank
            doc.Definition = new PollDefinition { Name = "Poll 1" };
            doc.EnsureRowSlots();
            ApplyFunctionUiState(doc.Definition.Function);
            UpdateDataSubtitle(doc.Definition);
            return;
        }
        _workspace.Close(doc);
    }

    // ────────── lifecycle (active poll) ──────────

    private async void OnConnect(object? sender, RoutedEventArgs e)
    {
        try
        {
            ConnectButton.IsEnabled = false;
            ApplyFunctionUiState(_document.Definition.Function);
            UpdateDataSubtitle(_document.Definition);
            await _document.StartAsync();
            SyncStatus();
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnStop(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _document.StopAsync();
            SyncStatus();
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    protected override async void OnClosed(EventArgs e)
    {
        try
        {
            if (_httpApi is not null) { try { await _httpApi.StopAsync(); } catch { } }
            foreach (var d in _workspace.Documents.ToList())
            {
                try { await d.StopAsync(); } catch { }
            }
            _workspace.Dispose();
        }
        catch { }
        base.OnClosed(e);
    }

    // ────────── status visuals ──────────

    private void ApplyStatusVisuals(StatusKind kind, string? text)
    {
        var (dotKey, surfaceKey, borderKey, fgKey) = kind switch
        {
            StatusKind.Ok  => ("StatusOkBrush",   "StatusOkSurfaceBrush",   "StatusOkBorderBrush",   "StatusOkBrush"),
            StatusKind.Err => ("StatusErrBrush",  "StatusErrSurfaceBrush",  "StatusErrBorderBrush",  "StatusErrBrush"),
            _              => ("StatusIdleBrush", "StatusIdleSurfaceBrush", "StatusIdleBorderBrush", "TextSecondaryBrush")
        };
        if (this.TryFindResource(dotKey, out var dot)     && dot is IBrush b1) StatusDot.Fill = b1;
        if (this.TryFindResource(surfaceKey, out var sur) && sur is IBrush b2) StatusPill.Background = b2;
        if (this.TryFindResource(borderKey, out var bd)   && bd  is IBrush b3) StatusPill.BorderBrush = b3;
        if (this.TryFindResource(fgKey, out var fg)       && fg  is IBrush b4) StatusText.Foreground = b4;
        StatusText.Text = text ?? "";
    }

    private void UpdateDataSubtitle(PollDefinition s)
    {
        var prefix = s.Function.Prefix();
        var endAddr = s.Address + Math.Max(0, s.Amount - 1);
        DataSubtitle.Text = $"{prefix} · {s.Address}..{endAddr} · {s.Amount} regs";
    }

    private void ApplyFunctionUiState(ModbusFunction function)
    {
        if (DataGrid.Columns.Count >= 3)
            DataGrid.Columns[2].IsReadOnly = !function.IsWritable();

        var isRegister = function.IsRegister();
        foreach (var mi in new[]
        {
            MenuItem16BitHeader, MenuItemSigned, MenuItemUnsigned, MenuItemHex, MenuItemBinary,
            MenuItem32BitHeader, MenuItemSigned32, MenuItemUnsigned32, MenuItemHex32, MenuItemFloat,
            MenuItem64BitHeader, MenuItemSigned64, MenuItemUnsigned64, MenuItemHex64, MenuItemDouble,
        })
        {
            mi.IsVisible = isRegister;
        }
    }

    // ────────── cell editing ──────────

    private void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.DataContext is RegisterRow row)
            _document.NotifyEditing(row);
    }

    private async void OnRowEditEnded(object? sender, DataGridRowEditEndedEventArgs e)
    {
        try
        {
            _document.NotifyEditCommitted();
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row.DataContext is not RegisterRow row) return;

            var def = _document.Definition;
            if (!def.Function.IsWritable())
            {
                RestoreDisplay(row, def);
                ApplyStatusVisuals(StatusKind.Err, "Function code is read-only");
                return;
            }

            ModbusResult result;
            switch (def.Function)
            {
                case ModbusFunction.Coils:
                    if (!ValueFormatter.TryParseCoil(row.Value, out var b))
                    {
                        RestoreDisplay(row, def);
                        ApplyStatusVisuals(StatusKind.Err, "Coil value must be 0/1");
                        return;
                    }
                    result = await _document.WriteCoilAsync(row.Address, b);
                    if (result.Success) row.RawBool = b;
                    break;

                case ModbusFunction.HoldingRegisters:
                    var stride = row.DataType.WordCount();
                    if (stride == 1)
                    {
                        if (!ValueFormatter.TryParseRegister(row.Value, row.DataType, out var raw))
                        {
                            RestoreDisplay(row, def);
                            ApplyStatusVisuals(StatusKind.Err, $"Invalid {row.DataType} value");
                            return;
                        }
                        result = await _document.WriteRegisterAsync(row.Address, raw);
                        if (result.Success) row.RawWords = new[] { raw };
                    }
                    else
                    {
                        if (!ValueFormatter.TryParseMultiRegister(row.Value, row.DataType, def.WordOrder, out var words))
                        {
                            RestoreDisplay(row, def);
                            ApplyStatusVisuals(StatusKind.Err, $"Invalid {row.DataType} value");
                            return;
                        }
                        result = await _document.WriteMultipleRegistersAsync(row.Address, words);
                        if (result.Success) row.RawWords = words;
                    }
                    break;

                default:
                    return;
            }

            RestoreDisplay(row, def);
            if (!result.Success) ApplyStatusVisuals(StatusKind.Err, result.Error);
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private static void RestoreDisplay(RegisterRow row, PollDefinition def)
    {
        row.Value = def.Function.IsRegister()
            ? ValueFormatter.FormatRegister(row.RawWords, row.DataType, def.WordOrder)
            : ValueFormatter.FormatCoil(row.RawBool);
    }

    // ────────── data type context menu ──────────

    private void OnTypeSigned(object? sender, RoutedEventArgs e)     => SetType(CellDataType.Signed);
    private void OnTypeUnsigned(object? sender, RoutedEventArgs e)   => SetType(CellDataType.Unsigned);
    private void OnTypeHex(object? sender, RoutedEventArgs e)        => SetType(CellDataType.Hex);
    private void OnTypeBinary(object? sender, RoutedEventArgs e)     => SetType(CellDataType.Binary);
    private void OnTypeSigned32(object? sender, RoutedEventArgs e)   => SetType(CellDataType.Signed32);
    private void OnTypeUnsigned32(object? sender, RoutedEventArgs e) => SetType(CellDataType.Unsigned32);
    private void OnTypeHex32(object? sender, RoutedEventArgs e)      => SetType(CellDataType.Hex32);
    private void OnTypeFloat(object? sender, RoutedEventArgs e)      => SetType(CellDataType.Float);
    private void OnTypeSigned64(object? sender, RoutedEventArgs e)   => SetType(CellDataType.Signed64);
    private void OnTypeUnsigned64(object? sender, RoutedEventArgs e) => SetType(CellDataType.Unsigned64);
    private void OnTypeHex64(object? sender, RoutedEventArgs e)      => SetType(CellDataType.Hex64);
    private void OnTypeDouble(object? sender, RoutedEventArgs e)     => SetType(CellDataType.Double);

    private void SetType(CellDataType type)
    {
        if (DataGrid.SelectedItem is not RegisterRow row) return;
        if (!_document.Definition.Function.IsRegister()) return;
        row.DataType = type;
        row.Value = ValueFormatter.FormatRegister(row.RawWords, type, _document.Definition.WordOrder);
    }

    // ────────── double-click → BinaryEditor ──────────

    private async void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (DataGrid.SelectedItem is not RegisterRow row) return;
            if (_document.Definition.Function != ModbusFunction.HoldingRegisters) return;
            if (row.DataType != CellDataType.Binary) return;

            var newWord = await new BinaryEditorView(row.RawInt).ShowDialog<int?>(this);
            if (newWord is not int word) return;

            var result = await _document.WriteRegisterAsync(row.Address, word);
            if (result.Success)
            {
                row.RawInt = word;
                row.Value = ValueFormatter.FormatRegister(word, CellDataType.Binary);
            }
            else
            {
                ApplyStatusVisuals(StatusKind.Err, result.Error);
            }
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    // ────────── menu handlers ──────────

    private async void OnOpenSetup(object? sender, RoutedEventArgs e)
    {
        try
        {
            await new SetupView().ShowDialog(this);
            // SettingsService.Current points at active doc's Definition, so no copy needed.
            _document.EnsureRowSlots();
            ApplyFunctionUiState(_document.Definition.Function);
            UpdateDataSubtitle(_document.Definition);
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnOpenConnectionSetup(object? sender, RoutedEventArgs e)
    {
        try { await new ConnectionSetupView().ShowDialog(this); }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnOpenColourRules(object? sender, RoutedEventArgs e)
    {
        try { await new ColourRulesView(_document.Definition).ShowDialog<bool>(this); }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnOpenFontPicker(object? sender, RoutedEventArgs e)
    {
        try
        {
            var changed = await new FontPickerView(_document.Definition).ShowDialog<bool>(this);
            if (changed) ApplyFontToGrid();
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private void ApplyFontToGrid()
    {
        var family = _document.Definition.DisplayFontFamily;
        var size = _document.Definition.DisplayFontSize;
        // Apply to all visible value-cell TextBlocks. The template column renders each row as a
        // TextBlock whose DataContext is the RegisterRow.
        foreach (var tb in DataGrid.GetVisualDescendants().OfType<TextBlock>())
        {
            if (tb.DataContext is RegisterRow)
            {
                if (!string.IsNullOrWhiteSpace(family))
                {
                    try { tb.FontFamily = new Avalonia.Media.FontFamily(family); } catch { }
                }
                if (size > 0) tb.FontSize = size;
            }
        }
    }

    private void OnOpenScraper(object? sender, RoutedEventArgs e)
    {
        try { new ModbusScraperView().Show(this); }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private void OnOpenChart(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_document.Rows.Count == 0)
            {
                ApplyStatusVisuals(StatusKind.Err, "Connect first to populate the grid");
                return;
            }

            var selected = DataGrid.SelectedItems
                .OfType<RegisterRow>()
                .Select(r => _document.Rows.IndexOf(r))
                .Where(i => i >= 0)
                .ToList();

            IReadOnlyList<int> indexes = selected.Count > 0
                ? selected
                : Enumerable.Range(0, Math.Min(5, _document.Rows.Count)).ToList();

            var function = _document.Definition.Function;
            Func<RegisterRow, double> sampler = function.IsRegister()
                ? row => row.RawInt
                : row => row.RawBool ? 1.0 : 0.0;

            var interval = TimeSpan.FromMilliseconds(
                Math.Max(200, _document.Definition.PollingRateMs));

            new LiveChartView(_document.Rows, indexes, sampler, interval).Show(this);
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnOpenTrafficMonitor(object? sender, RoutedEventArgs e)
    {
        try { await new TrafficMonitorView().ShowDialog(this); }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnOpenDeviceId(object? sender, RoutedEventArgs e)
    {
        try { await new DeviceIdView(_document.Definition).ShowDialog(this); }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnOpenTestCenter(object? sender, RoutedEventArgs e)
    {
        try { await new TestCenterView(_document.Definition).ShowDialog(this); }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnPrintToPdf(object? sender, RoutedEventArgs e)
    {
        try
        {
            var pick = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Print to PDF",
                DefaultExtension = "pdf",
                SuggestedFileName = $"{_document.Definition.Name}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } },
                },
            });
            if (pick is null) return;
            var rows = _document.Rows.Select(r =>
                (Address: r.DisplayAddress.ToString(), Value: r.Value ?? "", Type: r.DataType.ToString())
            ).ToList();
            PdfExporter.ExportRowsToPdf(pick.Path.LocalPath,
                title: $"OpenPoll — {_document.Definition.Name}",
                rows: rows);
            ApplyStatusVisuals(StatusKind.Ok, $"PDF saved → {pick.Path.LocalPath}");
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private XlsxSnapshotLogger? _xlsxLogger;
    private async void OnToggleXlsxSnapshot(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_xlsxLogger is { IsRunning: true })
            {
                await _xlsxLogger.StopAsync();
                ApplyStatusVisuals(StatusKind.Ok, $"XLSX snapshot saved → {_xlsxLogger.Path} ({_xlsxLogger.RowsWritten} rows)");
                _xlsxLogger = null;
                XlsxLogMenu.Header = "Start XLSX snapshot (1s)";
            }
            else
            {
                var path = XlsxSnapshotLogger.DefaultPath(_document.Definition.Name);
                _xlsxLogger = new XlsxSnapshotLogger(_document, intervalMs: 1000, path);
                _xlsxLogger.Start();
                ApplyStatusVisuals(StatusKind.Ok, $"XLSX snapshot → {path}");
                XlsxLogMenu.Header = "Stop XLSX snapshot";
            }
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private CsvSnapshotLogger? _csvLogger;
    private async void OnToggleCsvSnapshot(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_csvLogger is { IsRunning: true })
            {
                await _csvLogger.StopAsync();
                ApplyStatusVisuals(StatusKind.Ok, $"CSV snapshot saved → {_csvLogger.Path} ({_csvLogger.RowsWritten} rows)");
                _csvLogger = null;
                CsvLogMenu.Header = "Start CSV snapshot (1s)";
            }
            else
            {
                var path = CsvSnapshotLogger.DefaultPath(_document.Definition.Name);
                _csvLogger = new CsvSnapshotLogger(_document, intervalMs: 1000, path);
                _csvLogger.Start();
                ApplyStatusVisuals(StatusKind.Ok, $"CSV snapshot → {path}");
                CsvLogMenu.Header = "Stop CSV snapshot";
            }
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private void OnRevealLogFolder(object? sender, RoutedEventArgs e)
    {
        FileLogger.RevealLogFolder();
        ApplyStatusVisuals(StatusKind.Ok, $"Logs: {FileLogger.LogDirectory}");
    }

    private async void OnToggleHttpApi(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_httpApi is { IsRunning: true })
            {
                await _httpApi.StopAsync();
                HttpApiMenu.Header = "Start HTTP API (:8080)";
                ApplyStatusVisuals(StatusKind.Idle, "HTTP API stopped");
            }
            else
            {
                _httpApi ??= new HttpApiHost(_workspace);
                _httpApi.AuthToken = Environment.GetEnvironmentVariable("OPENPOLL_HTTP_TOKEN");
                await _httpApi.StartAsync(8080);
                HttpApiMenu.Header = $"Stop HTTP API ({_httpApi.BaseUrl})";
                var authNote = string.IsNullOrEmpty(_httpApi.AuthToken) ? "" : " · auth: Bearer";
                ApplyStatusVisuals(StatusKind.Ok, $"HTTP API on {_httpApi.BaseUrl}{authNote}");
            }
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnSaveWorkspace(object? sender, RoutedEventArgs e)
    {
        try { await WorkspaceFileService.SaveAsync(this, _workspace); RefreshRecentMenu(); }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnExportToSlave(object? sender, RoutedEventArgs e)
    {
        try
        {
            var pick = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export to OpenSlave workspace",
                DefaultExtension = "openslave",
                SuggestedFileName = $"{_document.Definition.Name}.openslave",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("OpenSlave workspace") { Patterns = new[] { "*.openslave" } },
                },
            });
            if (pick is null) return;
            PollToSlaveExporter.Export(pick.Path.LocalPath, _document.Definition);
            ApplyStatusVisuals(StatusKind.Ok, $"Slave workspace saved → {pick.Path.LocalPath}");
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }

    private async void OnLoadWorkspace(object? sender, RoutedEventArgs e)
    {
        try
        {
            var loaded = await WorkspaceFileService.OpenAsync(this);
            if (loaded is null) return;

            // Replace current workspace contents
            foreach (var d in _workspace.Documents.ToList())
            {
                try { await d.StopAsync(); } catch { }
                _workspace.Close(d);
            }
            foreach (var def in loaded)
                _workspace.AddNew(def);
            if (_workspace.Documents.Count > 0)
                _workspace.Active = _workspace.Documents[0];
            RefreshRecentMenu();
        }
        catch (Exception ex) { ApplyStatusVisuals(StatusKind.Err, ex.Message); }
    }
}
