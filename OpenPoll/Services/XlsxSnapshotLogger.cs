using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;

namespace OpenPoll.Services;

/// <summary>
/// Same shape as <see cref="CsvSnapshotLogger"/> but writes a real .xlsx workbook. We accumulate
/// rows in memory and save the file on <see cref="StopAsync"/> — Excel doesn't support streaming
/// append the way CSV does, so the trade-off is the buffer growing with session length. Fine for
/// hour-long sessions; users running multi-day captures should stick with CSV.
/// </summary>
public sealed class XlsxSnapshotLogger : IAsyncDisposable
{
    private readonly PollDocument _document;
    private readonly int _intervalMs;
    private readonly string _path;
    private readonly List<List<object>> _buffer = new();
    private CancellationTokenSource? _cts;
    private Task? _task;
    private long _rows;
    private string[]? _headers;

    public string Path => _path;
    public long RowsWritten => Interlocked.Read(ref _rows);
    public bool IsRunning => _task is { IsCompleted: false };

    public XlsxSnapshotLogger(PollDocument document, int intervalMs, string path)
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
        var safe = string.Concat((pollName ?? "poll").Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        return System.IO.Path.Combine(dir, $"openpoll-snapshot-{safe}-{stamp}.xlsx");
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
        Flush();
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var rows = _document.Rows.ToList();
        _headers = new[] { "timestamp_iso" }
            .Concat(rows.Select(r => "addr_" + r.DisplayAddress.ToString(CultureInfo.InvariantCulture)))
            .ToArray();

        while (!ct.IsCancellationRequested)
        {
            var data = new List<object> { DateTime.Now.ToString("o", CultureInfo.InvariantCulture) };
            foreach (var r in _document.Rows) data.Add(r.Value ?? "");
            lock (_buffer) _buffer.Add(data);
            Interlocked.Increment(ref _rows);
            try { await Task.Delay(_intervalMs, ct); }
            catch (TaskCanceledException) { break; }
        }
    }

    private void Flush()
    {
        if (_headers is null) return;
        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("snapshot");
            for (int c = 0; c < _headers.Length; c++)
                ws.Cell(1, c + 1).Value = _headers[c];
            lock (_buffer)
            {
                for (int r = 0; r < _buffer.Count; r++)
                {
                    var row = _buffer[r];
                    for (int c = 0; c < row.Count && c < _headers.Length; c++)
                        ws.Cell(r + 2, c + 1).Value = row[c]?.ToString();
                }
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(_path);
        }
        catch (Exception ex) { FileLogger.Error($"XlsxSnapshotLogger failed for {_path}: {ex.Message}"); }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
