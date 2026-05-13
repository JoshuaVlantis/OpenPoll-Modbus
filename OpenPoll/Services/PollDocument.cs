using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using OpenPoll.Models;

namespace OpenPoll.Services;

public enum PollStatus { Idle, Connecting, Connected, Error }

/// <summary>
/// Runtime state of a single Modbus poll: definition + session + rows + polling lifecycle.
/// One per tab. Decoupled from any UI control — exposes ObservableCollection and INPC.
/// </summary>
public sealed class PollDocument : INotifyPropertyChanged, IDisposable
{
    private readonly ModbusSession _session = new();

    /// <summary>Exposed so the HTTP API and other host-side callers can issue writes against this poll's transport.</summary>
    public ModbusSession Session => _session;
    private readonly object _stateLock = new();
    private CancellationTokenSource? _cts;
    private Task? _task;
    private RegisterRow? _editingRow;
    private bool _isStopped = true;

    private PollDefinition _definition;
    private PollStatus _status = PollStatus.Idle;
    private string _statusMessage = "Disconnected";
    private ulong _pollCount;

    public PollDocument(PollDefinition definition)
    {
        _definition = definition;
    }

    // ─── observable surface ─────────────────────────────────────────

    public PollDefinition Definition
    {
        get => _definition;
        set { _definition = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); }
    }

    public string Title => string.IsNullOrWhiteSpace(_definition.Name)
        ? $"{_definition.Function.Prefix()} · {_definition.IpAddress}:{_definition.ServerPort}"
        : _definition.Name;

    public ObservableCollection<RegisterRow> Rows { get; } = new();

    public PollStatus Status
    {
        get => _status;
        private set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); } }
    }

    public ulong PollCount
    {
        get => _pollCount;
        private set { if (_pollCount != value) { _pollCount = value; OnPropertyChanged(); } }
    }

    public bool IsRunning => _task is { IsCompleted: false };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

    // ─── lifecycle ──────────────────────────────────────────────────

    public async Task<ModbusResult> StartAsync()
    {
        await StopAsync();

        SetStatus(PollStatus.Connecting, "Connecting…");

        var connect = await Task.Run(() => _session.Connect(_definition));
        if (!connect.Success)
        {
            SetStatus(PollStatus.Error, connect.Error ?? "Connection failed");
            return connect;
        }

        SetStatus(PollStatus.Connected, "Connected");
        EnsureRowSlots();
        _isStopped = false;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _task = Task.Run(() => PollLoopAsync(token));
        return ModbusResult.Ok();
    }

    public async Task StopAsync()
    {
        _isStopped = true;
        if (_cts is not null)
        {
            _cts.Cancel();
            if (_task is not null)
            {
                try { await _task; } catch { /* ignore */ }
            }
            _cts.Dispose();
            _cts = null;
            _task = null;
        }
        await Task.Run(() => _session.Disconnect());
        SetStatus(PollStatus.Idle, "Disconnected");
    }

    public Task<ModbusResult> WriteCoilAsync(int address, bool value) =>
        Task.Run(() => _session.WriteSingleCoil(address, value));

    public Task<ModbusResult> WriteRegisterAsync(int address, int value) =>
        Task.Run(() => _session.WriteSingleRegister(address, value));

    public Task<ModbusResult> WriteMultipleCoilsAsync(int address, bool[] values) =>
        Task.Run(() => _session.WriteMultipleCoils(address, values));

    public Task<ModbusResult> WriteMultipleRegistersAsync(int address, int[] values) =>
        Task.Run(() => _session.WriteMultipleRegisters(address, values));

    // ─── edit tracking (suppress polling overwrites) ────────────────

    public void NotifyEditing(RegisterRow row) => _editingRow = row;
    public void NotifyEditCommitted() => _editingRow = null;

    // ─── polling loop ───────────────────────────────────────────────

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var def = _definition;

            // Always reconcile so settings changes mid-poll take effect.
            var connect = _session.Connect(def);
            if (!connect.Success)
            {
                PostStatus(PollStatus.Error, connect.Error ?? "Connection lost");
                if (await DelayAsync(def.PollingRateMs, ct)) break;
                continue;
            }

            await PollOnceAsync(def);

            if (await DelayAsync(def.PollingRateMs, ct)) break;
        }
    }

    private Task PollOnceAsync(PollDefinition def) => Task.Run(() =>
    {
        switch (def.Function)
        {
            case ModbusFunction.Coils:
            {
                var r = _session.ReadCoils(def.Address, def.Amount);
                if (!r.Success) { PostStatus(PollStatus.Error, r.Error); return; }
                PostApplyCoils(def, r.Value!); return;
            }
            case ModbusFunction.DiscreteInputs:
            {
                var r = _session.ReadDiscreteInputs(def.Address, def.Amount);
                if (!r.Success) { PostStatus(PollStatus.Error, r.Error); return; }
                PostApplyCoils(def, r.Value!); return;
            }
            case ModbusFunction.HoldingRegisters:
            {
                var r = _session.ReadHoldingRegisters(def.Address, def.Amount);
                if (!r.Success) { PostStatus(PollStatus.Error, r.Error); return; }
                PostApplyRegisters(def, r.Value!); return;
            }
            case ModbusFunction.InputRegisters:
            {
                var r = _session.ReadInputRegisters(def.Address, def.Amount);
                if (!r.Success) { PostStatus(PollStatus.Error, r.Error); return; }
                PostApplyRegisters(def, r.Value!); return;
            }
        }
    });

    private static async Task<bool> DelayAsync(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); return false; }
        catch (TaskCanceledException) { return true; }
    }

    private void PostApplyCoils(PollDefinition def, bool[] values) =>
        Dispatcher.UIThread.Post(() => ApplyCoils(def, values));

    private void PostApplyRegisters(PollDefinition def, int[] values) =>
        Dispatcher.UIThread.Post(() => ApplyRegisters(def, values));

    private void PostStatus(PollStatus kind, string? text)
    {
        var safe = text ?? "";
        Dispatcher.UIThread.Post(() =>
        {
            if (_isStopped) return;
            SetStatus(kind, safe);
        });
    }

    private void ApplyCoils(PollDefinition def, bool[] values)
    {
        if (_isStopped) return;
        EnsureRowSlots();
        var n = Math.Min(Rows.Count, values.Length);
        var prefix = def.Function.Prefix();
        for (int i = 0; i < n; i++)
        {
            var row = Rows[i];
            if (ReferenceEquals(row, _editingRow)) continue;
            row.Function = prefix;
            var wire = i + def.Address;
            row.Address = wire;
            row.DisplayAddress = wire + (def.DisplayOneIndexed ? 1 : 0);
            row.RawBool = values[i];
            row.Value = row.ApplyDisplayTransform(ValueFormatter.FormatCoil(values[i]));
            ApplyColourRule(row, def.ColourRules, values[i] ? 1.0 : 0.0);
        }
        PollCount++;
        SetStatus(PollStatus.Connected, "Connected");
    }

    private void ApplyRegisters(PollDefinition def, int[] values)
    {
        if (_isStopped) return;
        EnsureRowSlots();
        var n = Math.Min(Rows.Count, values.Length);
        var prefix = def.Function.Prefix();
        var order = def.WordOrder;
        for (int i = 0; i < n; i++)
        {
            var row = Rows[i];
            if (ReferenceEquals(row, _editingRow)) continue;
            row.Function = prefix;
            var wire = i + def.Address;
            row.Address = wire;
            row.DisplayAddress = wire + (def.DisplayOneIndexed ? 1 : 0);

            var stride = Math.Max(1, row.DataType.WordCount());
            var available = Math.Min(stride, values.Length - i);
            var packed = new int[available];
            for (int w = 0; w < available; w++) packed[w] = values[i + w];

            row.RawWords = packed;
            row.Value = row.ApplyDisplayTransform(ValueFormatter.FormatRegister(packed, row.DataType, order));
            ApplyColourRule(row, def.ColourRules, row.Value);
        }
        PollCount++;
        SetStatus(PollStatus.Connected, "Connected");
    }

    /// <summary>
    /// Evaluate the poll's colour rules against the (already-formatted) value string and update
    /// <see cref="RegisterRow.ForegroundHex"/>. Strings that don't parse as numbers (hex display,
    /// value-name labels) opt out of colouring.
    /// </summary>
    private static void ApplyColourRule(RegisterRow row, System.Collections.Generic.List<ColourRule>? rules, string display)
    {
        if (rules is null || rules.Count == 0) { if (row.ForegroundHex is not null) row.ForegroundHex = null; return; }
        var v = ParseNumeric(display);
        ApplyColourRule(row, rules, v);
    }

    private static void ApplyColourRule(RegisterRow row, System.Collections.Generic.List<ColourRule>? rules, double v)
    {
        if (rules is null || rules.Count == 0) { if (row.ForegroundHex is not null) row.ForegroundHex = null; return; }
        var hex = ColourRule.FirstMatch(rules, v);
        if (row.ForegroundHex != hex) row.ForegroundHex = hex;
    }

    private static double ParseNumeric(string s)
    {
        if (string.IsNullOrEmpty(s)) return double.NaN;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) return hex;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
        return double.NaN; // value-name labels / binary strings opt out
    }

    public void EnsureRowSlots()
    {
        var def = _definition;
        var prefix = def.Function.Prefix();
        if (Rows.Count != def.Amount)
        {
            Rows.Clear();
            _editingRow = null;
            for (int i = 0; i < def.Amount; i++)
            {
                Rows.Add(new RegisterRow
                {
                    Function = prefix,
                    Address = i + def.Address,
                    DisplayAddress = (i + def.Address) + (def.DisplayOneIndexed ? 1 : 0),
                    Value = "",
                    DataType = CellDataType.Signed
                });
            }
        }
    }

    private void SetStatus(PollStatus kind, string text)
    {
        Status = kind;
        StatusMessage = text;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _session.Dispose();
    }
}
