using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Views;

public partial class TrafficMonitorView : Window
{
    private bool _paused;

    public TrafficMonitorView()
    {
        InitializeComponent();
        EventGrid.ItemsSource = TrafficLog.Events;
        TrafficLog.EventRecorded += OnEventRecorded;
    }

    private void OnEventRecorded(TrafficEvent e)
    {
        if (_paused) return;
        if (AutoScrollInput.IsChecked == true && TrafficLog.Events.Count > 0)
        {
            // Defer scroll until layout pass
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                EventGrid.ScrollIntoView(TrafficLog.Events[^1], null);
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private void OnClear(object? sender, RoutedEventArgs e) => TrafficLog.Clear();

    private void OnTogglePause(object? sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "Resume" : "Pause";
    }

    protected override void OnClosed(EventArgs e)
    {
        TrafficLog.EventRecorded -= OnEventRecorded;
        base.OnClosed(e);
    }
}
