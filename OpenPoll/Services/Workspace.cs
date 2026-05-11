using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using OpenPoll.Models;

namespace OpenPoll.Services;

/// <summary>
/// In-memory workspace: the set of open polls and the currently-active one.
/// One per running app. Drives the tab strip in HomeView.
/// </summary>
public sealed class Workspace : INotifyPropertyChanged, IDisposable
{
    public ObservableCollection<PollDocument> Documents { get; } = new();

    private PollDocument? _active;
    public PollDocument? Active
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Active)));
        }
    }

    public PollDocument AddNew(PollDefinition? template = null, string? nameHint = null)
    {
        var def = (template ?? new PollDefinition()).Clone();
        if (string.IsNullOrWhiteSpace(def.Name))
            def.Name = nameHint ?? AutoName();
        var doc = new PollDocument(def);
        Documents.Add(doc);
        Active ??= doc;
        return doc;
    }

    public PollDocument AddExisting(PollDocument doc)
    {
        Documents.Add(doc);
        Active ??= doc;
        return doc;
    }

    public void Close(PollDocument doc)
    {
        var idx = Documents.IndexOf(doc);
        if (idx < 0) return;

        Documents.RemoveAt(idx);
        try { doc.Dispose(); } catch { }

        if (ReferenceEquals(_active, doc))
        {
            Active = Documents.Count == 0
                ? null
                : Documents[Math.Min(idx, Documents.Count - 1)];
        }
    }

    private string AutoName()
    {
        var n = 1;
        var taken = Documents.Select(d => d.Definition.Name).ToHashSet();
        while (taken.Contains($"Poll {n}")) n++;
        return $"Poll {n}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        foreach (var d in Documents.ToList())
        {
            try { d.Dispose(); } catch { }
        }
        Documents.Clear();
    }
}
