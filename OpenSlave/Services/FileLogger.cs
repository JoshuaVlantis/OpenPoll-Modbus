using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenSlave.Services;

/// <summary>
/// Append-only daily log file mirroring everything that hits the slave.
/// Always-on so a user reporting "lots of errors" can ship the file directly.
///
/// Path examples:
///   Linux  : ~/.local/share/OpenSlave/logs/openslave-2026-05-12.log
///   macOS  : ~/Library/Application Support/OpenSlave/logs/openslave-2026-05-12.log
///   Windows: %LOCALAPPDATA%\OpenSlave\logs\openslave-2026-05-12.log
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
            "OpenSlave", "logs");
        try
        {
            Directory.CreateDirectory(LogDirectory);
            RollIfNeeded();
            WriteRaw($"=== OpenSlave session start {DateTime.Now:O} (PID {Environment.ProcessId}) ===");
            Started = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FileLogger init failed: {ex.Message}");
        }
    }

    public static void Info(string message) => WriteLine("INFO", message);
    public static void Warn(string message) => WriteLine("WARN", message);
    public static void Error(string message) => WriteLine("ERROR", message);

    public static void Request(byte fc, int address, int quantity, string detail) =>
        WriteRaw($"{DateTime.Now:HH:mm:ss.fff}  REQ    FC{fc:X2}  @ {address,-7}  × {quantity,-4}  {detail}".TrimEnd());

    public static void ClientChange(int connectedClients) =>
        WriteLine("CLIENT", $"connected = {connectedClients}");

    private static void WriteLine(string level, string message) =>
        WriteRaw($"{DateTime.Now:HH:mm:ss.fff}  {level,-6} {message}");

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
        CurrentPath = Path.Combine(LogDirectory, $"openslave-{today}.log");
        var fs = new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(fs) { AutoFlush = true };
    }

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
        lock (_lock)
        {
            try { _writer?.WriteLine($"=== OpenSlave session end {DateTime.Now:O} ==="); _writer?.Dispose(); } catch { }
            _writer = null;
        }
        Started = false;
    }
}
