using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using OpenPoll.Models;

namespace OpenPoll.Services;

/// <summary>
/// Writes every Modbus traffic event (TX / RX / error) to a daily rotating
/// log file under the OS-standard local-app-data directory. Always-on so
/// when something goes wrong in the field the user can just send us the log.
///
/// Path examples:
///   Linux  : ~/.local/share/OpenPoll/logs/openpoll-2026-05-12.log
///   macOS  : ~/Library/Application Support/OpenPoll/logs/openpoll-2026-05-12.log
///   Windows: %LOCALAPPDATA%\OpenPoll\logs\openpoll-2026-05-12.log
/// </summary>
public static class FileLogger
{
    private static readonly object _lock = new();
    private static StreamWriter? _writer;
    private static string _currentDate = "";

    public static string LogDirectory { get; private set; } = "";
    public static string? CurrentPath { get; private set; }
    public static bool Started { get; private set; }

    public static void Start()
    {
        if (Started) return;
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenPoll", "logs");
        try
        {
            Directory.CreateDirectory(LogDirectory);
            RollIfNeeded();
            TrafficLog.EventRecorded += OnEvent;
            WriteRaw($"=== OpenPoll session start {DateTime.Now:O} (PID {Environment.ProcessId}) ===");
            Started = true;
        }
        catch (Exception ex)
        {
            // Logging must never break the app — surface to stderr and move on.
            Console.Error.WriteLine($"FileLogger init failed: {ex.Message}");
        }
    }

    public static void Info(string message) => WriteRaw($"{DateTime.Now:HH:mm:ss.fff}  INFO   {message}");
    public static void Warn(string message) => WriteRaw($"{DateTime.Now:HH:mm:ss.fff}  WARN   {message}");
    public static void Error(string message) => WriteRaw($"{DateTime.Now:HH:mm:ss.fff}  ERROR  {message}");

    private static void OnEvent(TrafficEvent ev)
    {
        var line = $"{ev.TimestampDisplay}  {ev.DirectionDisplay,-3}  {ev.FunctionDisplay,-26}  @ {ev.AddressDisplay,-7}  × {ev.Quantity,-4}  {ev.Detail}".TrimEnd();
        WriteRaw(line);
    }

    private static void WriteRaw(string line)
    {
        lock (_lock)
        {
            try
            {
                RollIfNeeded();
                _writer?.WriteLine(line);
            }
            catch { /* swallow */ }
        }
    }

    private static void RollIfNeeded()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (today == _currentDate && _writer is not null) return;

        try { _writer?.Dispose(); } catch { }
        _currentDate = today;
        CurrentPath = Path.Combine(LogDirectory, $"openpoll-{today}.log");
        var fs = new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(fs) { AutoFlush = true };
    }

    /// <summary>Open the log folder in the OS file manager.</summary>
    public static void RevealLogFolder()
    {
        if (string.IsNullOrEmpty(LogDirectory)) return;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo("explorer.exe", LogDirectory) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start(new ProcessStartInfo("open", LogDirectory) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo("xdg-open", LogDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Error($"RevealLogFolder failed: {ex.Message}");
        }
    }

    public static void Stop()
    {
        if (!Started) return;
        TrafficLog.EventRecorded -= OnEvent;
        lock (_lock)
        {
            try { _writer?.WriteLine($"=== OpenPoll session end {DateTime.Now:O} ==="); _writer?.Dispose(); } catch { }
            _writer = null;
        }
        Started = false;
    }
}
