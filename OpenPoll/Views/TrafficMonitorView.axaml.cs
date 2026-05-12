using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        var sp = StorageProvider;
        var pick = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save traffic log",
            DefaultExtension = "log",
            SuggestedFileName = $"openpoll-traffic-{DateTime.Now:yyyyMMdd-HHmmss}.log",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text log") { Patterns = new[] { "*.log", "*.txt" } },
                new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } },
            },
        });
        if (pick is null) return;

        var path = pick.Path.LocalPath;
        var isCsv = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        if (isCsv) sb.AppendLine("time,direction,function,address,quantity,detail");
        foreach (var ev in TrafficLog.Events.ToArray())
        {
            if (isCsv)
                sb.AppendLine($"{ev.TimestampDisplay},{ev.DirectionDisplay},{ev.FunctionDisplay},{ev.AddressDisplay},{ev.Quantity},\"{(ev.Detail ?? "").Replace("\"", "\"\"")}\"");
            else
                sb.AppendLine($"{ev.TimestampDisplay}  {ev.DirectionDisplay}  {ev.FunctionDisplay,-26}  @ {ev.AddressDisplay,-6}  × {ev.Quantity,-4}  {ev.Detail}");
        }
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    protected override void OnClosed(EventArgs e)
    {
        TrafficLog.EventRecorded -= OnEventRecorded;
        base.OnClosed(e);
    }
}
