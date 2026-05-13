using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using OpenPoll.Models;

namespace OpenPoll.Views;

public partial class LiveChartView : Window
{
    private readonly IReadOnlyList<RegisterRow> _rows;
    private readonly IReadOnlyList<int> _targetIndexes;
    private readonly Func<RegisterRow, double> _sampler;
    private readonly ObservableCollection<DateTimePoint>[] _values;
    private readonly DispatcherTimer _timer;
    private TimeSpan _window = TimeSpan.FromMinutes(5);
    private bool _paused;

    public LiveChartView() : this(Array.Empty<RegisterRow>(), Array.Empty<int>(), _ => 0) { }

    public LiveChartView(
        IReadOnlyList<RegisterRow> rows,
        IReadOnlyList<int> targetIndexes,
        Func<RegisterRow, double> sampler,
        TimeSpan? sampleInterval = null)
    {
        InitializeComponent();

        _rows = rows;
        _targetIndexes = targetIndexes;
        _sampler = sampler;

        _values = new ObservableCollection<DateTimePoint>[targetIndexes.Count];
        for (int i = 0; i < _values.Length; i++)
            _values[i] = new ObservableCollection<DateTimePoint>();

        _timer = new DispatcherTimer { Interval = sampleInterval ?? TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;

        if (Design.IsDesignMode) return;

        Chart.Series = BuildSeries();
        Chart.XAxes = BuildXAxes();
        Chart.YAxes = new[] { new Axis() };   // single Y axis owned by us so we can mutate limits
        _timer.Start();

        StatusText.Text = $"Tracking {targetIndexes.Count} row(s) every {(int)_timer.Interval.TotalMilliseconds}ms";
    }

    private void OnApplyYRange(object? sender, RoutedEventArgs e)
    {
        if (Chart.YAxes is not IEnumerable<Axis> axes) return;
        var yAxis = axes.FirstOrDefault();
        if (yAxis is null) return;
        yAxis.MinLimit = ParseDouble(YMinInput.Text);
        yAxis.MaxLimit = ParseDouble(YMaxInput.Text);
        StatusText.Text = $"Y range: {Format(yAxis.MinLimit)}..{Format(yAxis.MaxLimit)}";
    }

    private static double? ParseDouble(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string Format(double? v) =>
        v is null ? "auto" : v.Value.ToString("G6", CultureInfo.InvariantCulture);

    private async void OnExportPng(object? sender, RoutedEventArgs e)
    {
        try
        {
            var pick = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export chart as PNG",
                DefaultExtension = "png",
                SuggestedFileName = "chart.png",
                FileTypeChoices = new[] { new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } } },
            });
            if (pick is null) return;

            var size = new PixelSize(Math.Max(640, (int)Chart.Bounds.Width),
                                     Math.Max(360, (int)Chart.Bounds.Height));
            using var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
            bmp.Render(Chart);
            await using var stream = await pick.OpenWriteAsync();
            bmp.Save(stream);
            StatusText.Text = $"Saved {pick.Path.LocalPath}";
        }
        catch (Exception ex) { StatusText.Text = "Export failed: " + ex.Message; }
    }

    private async void OnExportCsv(object? sender, RoutedEventArgs e)
    {
        try
        {
            var pick = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export chart data as CSV",
                DefaultExtension = "csv",
                SuggestedFileName = "chart.csv",
                FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } },
            });
            if (pick is null) return;
            var csv = BuildCsv();
            await using var stream = await pick.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(csv);
            StatusText.Text = $"Saved {pick.Path.LocalPath}";
        }
        catch (Exception ex) { StatusText.Text = "Export failed: " + ex.Message; }
    }

    private string BuildCsv()
    {
        var headers = new List<string> { "timestamp_iso" };
        for (int i = 0; i < _targetIndexes.Count; i++)
        {
            var idx = _targetIndexes[i];
            headers.Add(idx < _rows.Count ? "addr_" + _rows[idx].Address : "row_" + idx);
        }
        // Build a union of all timestamps so multi-series with sparse data still align.
        var stamps = new SortedSet<DateTime>();
        foreach (var series in _values)
            foreach (var p in series)
                stamps.Add(p.DateTime);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(",", headers));
        foreach (var t in stamps)
        {
            sb.Append(t.ToString("o", CultureInfo.InvariantCulture));
            for (int i = 0; i < _values.Length; i++)
            {
                sb.Append(',');
                var point = _values[i].FirstOrDefault(p => p.DateTime == t);
                if (point?.Value is double v)
                    sb.Append(v.ToString(CultureInfo.InvariantCulture));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private ISeries[] BuildSeries()
    {
        var series = new ISeries[_targetIndexes.Count];
        for (int i = 0; i < _targetIndexes.Count; i++)
        {
            var idx = _targetIndexes[i];
            var title = idx < _rows.Count ? $"Address {_rows[idx].Address}" : $"Row {idx}";
            series[i] = new LineSeries<DateTimePoint>
            {
                Name = title,
                Values = _values[i],
                GeometrySize = 0,
                LineSmoothness = 0
            };
        }
        return series;
    }

    private Axis[] BuildXAxes() => new Axis[]
    {
        new DateTimeAxis(TimeSpan.FromSeconds(1), d => d.ToString("HH:mm:ss"))
        {
            LabelsRotation = 0
        }
    };

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_paused) return;
        var now = DateTime.Now;
        var cutoff = now - _window;

        for (int i = 0; i < _targetIndexes.Count; i++)
        {
            var idx = _targetIndexes[i];
            if (idx < 0 || idx >= _rows.Count) continue;
            var value = _sampler(_rows[idx]);
            _values[i].Add(new DateTimePoint(now, value));

            while (_values[i].Count > 0 && _values[i][0].DateTime < cutoff)
                _values[i].RemoveAt(0);
        }
    }

    private void OnTogglePause(object? sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "Resume" : "Pause";
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        foreach (var v in _values) v.Clear();
    }

    private void OnWindowChanged(object? sender, SelectionChangedEventArgs e)
    {
        _window = WindowInput.SelectedIndex switch
        {
            0 => TimeSpan.FromSeconds(60),
            1 => TimeSpan.FromMinutes(5),
            2 => TimeSpan.FromMinutes(15),
            3 => TimeSpan.FromHours(1),
            _ => TimeSpan.FromMinutes(5)
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
