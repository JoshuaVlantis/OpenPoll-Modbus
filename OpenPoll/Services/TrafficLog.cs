using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using OpenPoll.Models;

namespace OpenPoll.Services;

/// <summary>
/// Process-wide circular log of Modbus traffic events.
/// All ModbusSession instances feed into this; TrafficMonitorView observes it.
/// </summary>
public static class TrafficLog
{
    private const int MaxEvents = 5000;

    public static ObservableCollection<TrafficEvent> Events { get; } = new();

    public static event Action<TrafficEvent>? EventRecorded;

    public static void Record(TrafficEvent ev)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Events.Add(ev);
            while (Events.Count > MaxEvents) Events.RemoveAt(0);
            EventRecorded?.Invoke(ev);
        });
    }

    public static void Clear()
    {
        Dispatcher.UIThread.Post(() => Events.Clear());
    }
}
