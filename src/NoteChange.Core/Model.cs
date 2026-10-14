using System.Text.Json.Serialization;

namespace NoteChange.Core;

/// <summary>A note as it arrived: from iCloud, the clipboard, a file or a project. This is what projects store.</summary>
public sealed class NoteSource
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("html")] public string? Html { get; set; }
    [JsonPropertyName("origin")] public string Origin { get; set; } = "paste";
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = new();

    public static NoteSource FromText(string text, string origin = "paste") => new() { Text = text ?? "", Origin = origin };

    public static NoteSource FromHtml(string html, string text, string origin) => new() { Html = html, Text = text ?? "", Origin = origin };

    public string OriginLabel => Origin switch
    {
        "icloud" => "iCloud note",
        "example" => "Example",
        "file" => "From a file",
        _ => "Pasted note"
    };
}

/// <summary>A picture inside a note. Only PNG and JPEG are kept; everything else becomes a placeholder.</summary>
public sealed class NoteImage
{
    public required byte[] Data { get; init; }
    /// <summary>"png" or "jpeg".</summary>
    public required string Format { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string Alt { get; init; } = "Picture";

    public string MimeType => "image/" + Format;
    public string DataUri => "data:" + MimeType + ";base64," + Convert.ToBase64String(Data);
}

/// <summary>A piece of text with one set of formatting, or a picture.</summary>
public sealed class Run
{
    public string Text { get; set; } = "";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strike { get; set; }
    public string? Link { get; set; }
    public NoteImage? Image { get; set; }

    public bool IsImage => Image != null;
    public bool HasText => !IsImage && !string.IsNullOrWhiteSpace(Text);

    public Run With(string text) => new()
    {
        Text = text, Bold = Bold, Italic = Italic, Underline = Underline, Strike = Strike, Link = Link
    };

    public bool SameStyle(Run other) =>
        !IsImage && !other.IsImage && Bold == other.Bold && Italic == other.Italic &&
        Underline == other.Underline && Strike == other.Strike && Link == other.Link;
}

public enum BlockKind { Paragraph, Heading, ListItem, Table }

public sealed class TableCell
{
    public bool Header { get; set; }
    public List<Run> Runs { get; set; } = new();
}

/// <summary>One paragraph, heading, list item or table.</summary>
public sealed class Block
{
    public BlockKind Kind { get; set; }
    /// <summary>Heading level (1-6) or list nesting level (0-8).</summary>
    public int Level { get; set; }
    public bool Ordered { get; set; }
    /// <summary>Items in the same list share a group so numbering restarts per list.</summary>
    public int Group { get; set; }
    public int Number { get; set; }
    /// <summary>A checklist item: the ☐ or ☑ is part of the text, so no bullet is drawn.</summary>
    public bool Checklist { get; set; }
    public List<Run> Runs { get; set; } = new();
    public List<List<TableCell>> Rows { get; set; } = new();

    public string PlainText => string.Concat(Runs.Select(r => r.IsImage ? "" : r.Text));
    public bool HasContent => Kind == BlockKind.Table ? Rows.Count > 0 : Runs.Any(r => r.IsImage || r.HasText);

    public Block Clone() => new()
    {
        Kind = Kind, Level = Level, Ordered = Ordered, Group = Group, Number = Number, Checklist = Checklist,
        Runs = Runs.Select(CloneRun).ToList(),
        Rows = Rows.Select(row => row.Select(c => new TableCell { Header = c.Header, Runs = c.Runs.Select(CloneRun).ToList() }).ToList()).ToList()
    };

    private static Run CloneRun(Run r) => new()
    {
        Text = r.Text, Bold = r.Bold, Italic = r.Italic, Underline = r.Underline, Strike = r.Strike, Link = r.Link, Image = r.Image
    };
}

/// <summary>A note after import: clean blocks, ready to be laid out.</summary>
public sealed class ImportedNote
{
    public List<Block> Blocks { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    /// <summary>The first line of the note, used as its name in the collection.</summary>
    public string Title { get; init; } = "";
}

public enum Layout { Auto, Document, Recipe, Chapter }

public enum TitleMode { FirstLine, Custom, None }

public sealed class FormatOptions
{
    public Layout Layout { get; set; } = Layout.Auto;
    public TitleMode TitleMode { get; set; } = TitleMode.FirstLine;
    public string CustomTitle { get; set; } = "";
    public bool LargeText { get; set; } = true;
}

/// <summary>A note laid out as a document: title, body, layout and type size.</summary>
public sealed class FormattedNote
{
    public string Title { get; init; } = "";
    public List<Block> Blocks { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    /// <summary>The layout actually used (never Auto).</summary>
    public Layout Layout { get; init; } = Layout.Document;
    /// <summary>Body text size in points.</summary>
    public int FontSize { get; init; } = 12;
    public string Font => Layout == Layout.Chapter ? "Georgia" : "Calibri";

    public string LayoutLabel => Layout switch
    {
        Layout.Recipe => "Recipe layout",
        Layout.Chapter => "Chapter layout",
        _ => "Clean document"
    };
}

/// <summary>Thrown with a message that is safe and useful to show to the person.</summary>
public sealed class NoteException : Exception
{
    public NoteException(string message) : base(message) { }
}
