using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NoteChange.Core;

/// <summary>A saved collection of notes (.notechange.json). Compatible with files from earlier versions.</summary>
public sealed class Project
{
    public const long MaxBytes = 32 * 1024 * 1024;

    [JsonPropertyName("type")] public string Type { get; set; } = "notechange-project";
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("notes")] public List<NoteSource> Notes { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Serialize(IEnumerable<NoteSource> notes) =>
        JsonSerializer.Serialize(new Project { Notes = notes.ToList() }, Options);

    public static List<NoteSource> Parse(string json)
    {
        Project? p;
        try { p = JsonSerializer.Deserialize<Project>(json); }
        catch (JsonException) { throw new NoteException("This file isn't a NoteChange project, or it's damaged."); }
        if (p == null || p.Type != "notechange-project" || p.Version != 1 || p.Notes == null)
            throw new NoteException("This file isn't a NoteChange project, or it's damaged.");
        if (p.Notes.Count > Formatter.MaxNotes)
            throw new NoteException("This project has more than " + Formatter.MaxNotes + " notes.");
        foreach (var n in p.Notes)
        {
            n.Text ??= "";
            n.Warnings ??= new List<string>();
            if (n.Origin is not ("icloud" or "paste" or "example" or "file")) n.Origin = "paste";
        }
        return p.Notes;
    }
}

public static class FileNames
{
    /// <summary>A file name that Windows accepts, based on the note's title.</summary>
    public static string Safe(string? value, string fallback = "My note")
    {
        string name = Regex.Replace(value ?? "", @"[<>:""/\\|?*\x00-\x1f]", "_").Trim();
        name = Regex.Replace(name, @"[. ]+$", "");
        if (name.Length > 100) name = name[..100].TrimEnd(' ', '.');
        if (name.Length == 0) name = fallback;
        if (Regex.IsMatch(name, @"^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\.|$)", RegexOptions.IgnoreCase)) name = "_" + name;
        return name;
    }
}
