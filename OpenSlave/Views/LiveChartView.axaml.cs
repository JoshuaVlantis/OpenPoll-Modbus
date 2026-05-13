using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using OpenSlave.Models;

namespace OpenSlave.Views;

/// <summary>
/// Real-time line chart for selected slave register values. Mirrors OpenPoll's chart view but
/// reads from the slave's RegisterCell collection on a tick timer.
/// </summary>
public partial class LiveChartView : Window
{
    private readonly IReadOnlyList<RegisterCell> _cells;
    private readonly IReadOnlyList<int> _targetIndexes;
    private readonly ObservableCollection<DateTimePoint>[] _values;
    private readonly DispatcherTimer _timer;
    private TimeSpan _window = TimeSpan.FromMinutes(5);
    private bool _paused;

    public LiveChartView() : this(Array.Empty<RegisterCell>(), Array.Empty<int>()) { }

    public LiveChartView(IReadOnlyList<RegisterCell> cells, IReadOnlyList<int> targetIndexes, TimeSpan? sampleInterval = null)
    {
        InitializeComponent();
        _cells = cells;
        _targetIndexes = targetIndexes;

        _values = new ObservableCollection<DateTimePoint>[targetIndexes.Count];
        for (int i = 0; i < _values.Length; i++) _values[i] = new ObservableCollection<DateTimePoint>();

        _timer = new DispatcherTimer { Interval = sampleInterval ?? TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;

        if (Design.IsDesignMode) return;

        Chart.Series = BuildSeries();
        Chart.XAxes = new Axis[]
        {
            new DateTimeAxis(TimeSpan.FromSeconds(1), d => d.ToString("HH:mm:ss")),
        };
        _timer.Start();
        StatusText.Text = $"Tracking {targetIndexes.Count} cell(s) · zoom X-axis with mouse wheel";
    }

    private ISeries[] BuildSeries()
    {
        var series = new ISeries[_targetIndexes.Count];
        for (int i = 0; i < _targetIndexes.Count; i++)
        {
            var idx = _targetIndexes[i];
            var title = idx < _cells.Count ? $"Address {_cells[idx].Address}" : $"Cell {idx}";
            series[i] = new LineSeries<DateTimePoint>
            {
                Name = title, Values = _values[i], GeometrySize = 0, LineSmoothness = 0,
            };
        }
        return series;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_paused) return;
        var now = DateTime.Now;
        var cutoff = now - _window;

        for (int i = 0; i < _targetIndexes.Count; i++)
        {
            var idx = _targetIndexes[i];
            if (idx < 0 || idx >= _cells.Count) continue;
            double v = _cells[idx].RawValue;
            _values[i].Add(new DateTimePoint(now, v));
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
            _ => TimeSpan.FromMinutes(5),
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
