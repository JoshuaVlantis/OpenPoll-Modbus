using System;

namespace OpenPoll.Models;

public enum TrafficDirection { Tx, Rx, Error }

public sealed class TrafficEvent
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public TrafficDirection Direction { get; init; }
    public string Function { get; init; } = "";
    public int? Address { get; init; }
    public int? Quantity { get; init; }
    public string Detail { get; init; } = "";

    public string TimestampDisplay => Timestamp.ToString("HH:mm:ss.fff");
    public string DirectionDisplay => Direction switch
    {
        TrafficDirection.Tx => "→",
        TrafficDirection.Rx => "←",
        _ => "✗"
    };
    public string FunctionDisplay => Function;
    public string AddressDisplay => Address.HasValue
        ? (Quantity.HasValue && Quantity.Value > 1
            ? $"{Address}×{Quantity}"
            : Address.Value.ToString())
        : "";
}
