using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using OpenPoll.Models;

namespace OpenPoll.Services;

/// <summary>
/// Serializes the open polls in a Workspace to a `.openpoll` JSON file and back.
/// File format is forward-compatible: unknown fields are ignored on load.
/// </summary>
public static class WorkspaceFileService
{
    private const string Extension = "openpoll";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public sealed record WorkspaceFile(int Version, List<PollDefinition> Polls);

    public static async Task SaveAsync(Window owner, Workspace workspace)
    {
        var picker = owner.StorageProvider;
        var file = await picker.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save workspace",
            SuggestedFileName = "workspace",
            DefaultExtension = Extension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("OpenPoll workspace") { Patterns = new[] { "*." + Extension } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        });
        if (file is null) return;

        var payload = new WorkspaceFile(
            Version: 1,
            Polls: workspace.Documents.Select(d => d.Definition).ToList());

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(json);

        RecentFilesService.Add(file.Path.LocalPath);
    }

    public static async Task<IReadOnlyList<PollDefinition>?> OpenAsync(Window owner)
    {
        var picker = owner.StorageProvider;
        var files = await picker.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open workspace",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("OpenPoll workspace") { Patterns = new[] { "*." + Extension } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        });
        if (files.Count == 0) return null;

        var polls = await LoadFromPathAsync(files[0].Path.LocalPath);
        return polls;
    }

    /// <summary>Open by explicit path (used by the MRU menu).</summary>
    public static async Task<IReadOnlyList<PollDefinition>?> LoadFromPathAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        var parsed = JsonSerializer.Deserialize<WorkspaceFile>(json, JsonOptions);
        if (parsed is null) throw new InvalidDataException("Workspace file is empty or invalid JSON.");
        if (parsed.Version > 1) throw new InvalidDataException($"Workspace version {parsed.Version} is newer than this app supports.");

        RecentFilesService.Add(path);
        return parsed.Polls;
    }
}
