using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenPoll.Services;

/// <summary>
/// Most-recently-used workspace file list, persisted to %APPDATA%/OpenPoll/recent.json.
/// Standard File-menu MRU: 10 entries, newest first.
/// </summary>
public static class RecentFilesService
{
    public const int MaxEntries = 10;

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OpenPoll");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "recent.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new List<string>();
            var json = File.ReadAllText(ConfigPath);
            var doc = JsonSerializer.Deserialize<RecentDoc>(json, Options);
            return doc?.Files?.Where(File.Exists).Take(MaxEntries).ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load recent files: {ex.Message}");
            return new List<string>();
        }
    }

    public static void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var list = Load();
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        Save(list);
    }

    public static void Clear() => Save(new List<string>());

    private static void Save(List<string> list)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(new RecentDoc { Files = list }, Options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save recent files: {ex.Message}");
        }
    }

    private sealed class RecentDoc
    {
        public List<string> Files { get; set; } = new();
    }
}
