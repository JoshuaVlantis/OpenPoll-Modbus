using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPoll.Models;

namespace OpenPoll.Services;

public static class SettingsService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OpenPoll");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static PollDefinition Current { get; private set; } = Load();

    /// <summary>Re-targets <see cref="Current"/> at the given definition (typically the active poll's).
    /// Dialogs that read/write <see cref="Current"/> then operate on the active poll's definition by reference.</summary>
    public static void SetCurrent(PollDefinition def) => Current = def;

    public static PollDefinition Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<PollDefinition>(json, JsonOptions);
                if (loaded is not null)
                {
                    Current = loaded;
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load settings from {ConfigPath}: {ex.Message}");
        }

        Current = new PollDefinition();
        return Current;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save settings to {ConfigPath}: {ex.Message}");
        }
    }

    public static string ConfigFilePath => ConfigPath;
}
