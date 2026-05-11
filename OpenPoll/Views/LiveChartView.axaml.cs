using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
        _timer.Start();

        StatusText.Text = $"Tracking {targetIndexes.Count} row(s) every {(int)_timer.Interval.TotalMilliseconds}ms";
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
