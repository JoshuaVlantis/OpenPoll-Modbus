using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenPoll.Services;

/// <summary>
/// Periodically samples a <see cref="PollDocument"/>'s row values and appends them as a wide CSV
/// row (one column per row address). Independent of the event-based <see cref="FileLogger"/> —
/// this is fixed-interval data capture for offline analysis.
/// </summary>
public sealed class CsvSnapshotLogger : IAsyncDisposable
{
    private readonly PollDocument _document;
    private readonly int _intervalMs;
    private readonly string _path;
    private CancellationTokenSource? _cts;
    private Task? _task;
    private long _rows;

    public string Path => _path;
    public long RowsWritten => Interlocked.Read(ref _rows);
    public bool IsRunning => _task is { IsCompleted: false };

    public CsvSnapshotLogger(PollDocument document, int intervalMs, string path)
    {
        if (intervalMs < 50) intervalMs = 50;
        _document = document;
        _intervalMs = intervalMs;
        _path = path;
    }

    public static string DefaultPath(string pollName)
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenPoll", "logs");
        Directory.CreateDirectory(dir);
        var safeName = string.Concat((pollName ?? "poll").Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        return System.IO.Path.Combine(dir, $"openpoll-snapshot-{safeName}-{stamp}.csv");
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _task = Task.Run(() => LoopAsync(ct));
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        _cts.Cancel();
        try { if (_task is not null) await _task; } catch { }
        _cts.Dispose();
        _cts = null;
        _task = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        try
        {
            await using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream) { AutoFlush = false };

            // Header line — best-effort snapshot of the row addresses at start time.
            var rows = _document.Rows.ToList();
            var header = new List<string> { "timestamp_iso" };
            foreach (var r in rows) header.Add("addr_" + r.DisplayAddress.ToString(CultureInfo.InvariantCulture));
            await writer.WriteLineAsync(string.Join(",", header));

            while (!ct.IsCancellationRequested)
            {
                var fields = new List<string> { DateTime.Now.ToString("o", CultureInfo.InvariantCulture) };
                // Re-read live rows each tick — they may have grown/shrunk if the user changed Amount.
                foreach (var r in _document.Rows) fields.Add(CsvEscape(r.Value));
                await writer.WriteLineAsync(string.Join(",", fields));
                await writer.FlushAsync();
                Interlocked.Increment(ref _rows);
                try { await Task.Delay(_intervalMs, ct); }
                catch (TaskCanceledException) { break; }
            }
        }
        catch (Exception ex)
        {
            FileLogger.Error($"CsvSnapshotLogger failed for {_path}: {ex.Message}");
        }
    }

    private static string CsvEscape(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
