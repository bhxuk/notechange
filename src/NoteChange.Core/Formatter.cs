using System.Text.RegularExpressions;

namespace NoteChange.Core;

/// <summary>
/// Lays a note out as a document. Words and their order never change: the formatter only picks a
/// title, chooses a layout and turns recognised recipe labels into headings.
/// </summary>
public static class Formatter
{
    public const int MaxNotes = 100;

    private static readonly Regex RecipeHint = new(@"^\s*(ingredients|method|instructions|directions)\s*:?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex ChapterHint = new(@"^\s*(chapter\s+\S+|prologue|epilogue|part\s+(one|two|three|four|five|\d+))\b", RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex RecipeLabel = new(@"^(ingredients|method|instructions|directions|preparation|to serve|notes|tips|serves\s*\d*|servings|equipment|for the [a-z ]{2,30})\s*:?$", RegexOptions.IgnoreCase);

    public static FormattedNote Format(ImportedNote note, FormatOptions options)
    {
        options ??= new FormatOptions();
        var blocks = note.Blocks.Select(b => b.Clone()).ToList();
        string title = "";

        if (options.TitleMode == TitleMode.Custom)
        {
            title = (options.CustomTitle ?? "").Trim();
        }
        else if (options.TitleMode == TitleMode.FirstLine)
        {
            int i = blocks.FindIndex(b => b.Kind != BlockKind.Table && b.Runs.Any(r => r.HasText));
            if (i >= 0) title = TakeFirstLine(blocks, i);
        }

        string allText = string.Join("\n", new[] { title }.Concat(blocks.Select(b => b.PlainText)));
        var layout = options.Layout;
        if (layout == Layout.Auto)
            layout = RecipeHint.IsMatch(allText) ? Layout.Recipe : ChapterHint.IsMatch(allText) ? Layout.Chapter : Layout.Document;

        if (layout == Layout.Recipe)
        {
            foreach (var b in blocks)
            {
                if (b.Kind == BlockKind.Paragraph && b.Runs.All(r => !r.IsImage) && RecipeLabel.IsMatch(b.PlainText.Trim()))
                {
                    b.Kind = BlockKind.Heading;
                    b.Level = 2;
                }
            }
        }

        return new FormattedNote
        {
            Title = title,
            Blocks = blocks,
            Warnings = note.Warnings.ToList(),
            Layout = layout,
            FontSize = options.LargeText ? 14 : 12
        };
    }

    /// <summary>Removes the first line of block <paramref name="index"/> and returns it as the title.</summary>
    private static string TakeFirstLine(List<Block> blocks, int index)
    {
        var block = blocks[index];
        var title = new System.Text.StringBuilder();
        var rest = new List<Run>();
        bool done = false;
        foreach (var run in block.Runs)
        {
            if (done || run.IsImage)
            {
                // A picture before the first line break stays in the body.
                rest.Add(run);
                continue;
            }
            int nl = run.Text.IndexOf('\n');
            if (nl < 0) { title.Append(run.Text); continue; }
            title.Append(run.Text, 0, nl);
            done = true;
            string after = run.Text[(nl + 1)..];
            if (after.Length > 0) rest.Add(run.With(after));
        }

        Importer.Tidy(rest);
        if (rest.Any(r => r.IsImage || r.HasText))
        {
            block.Runs = rest;
            if (block.Kind == BlockKind.Heading) block.Kind = BlockKind.Paragraph;
        }
        else blocks.RemoveAt(index);

        return title.ToString().Trim();
    }

    public static List<FormattedNote> FormatAll(IReadOnlyList<NoteSource> sources, FormatOptions options)
    {
        if (sources == null || sources.Count == 0) throw new NoteException("Add a note first.");
        if (sources.Count > MaxNotes) throw new NoteException("Choose up to " + MaxNotes + " notes at a time.");
        return sources.Select(s => Format(Importer.Import(s), options)).ToList();
    }

    /// <summary>Joins a collection into one note for formats with no page breaks (text, Markdown, email).</summary>
    public static FormattedNote Flatten(IReadOnlyList<FormattedNote> notes)
    {
        if (notes.Count == 1) return notes[0];
        var blocks = new List<Block>();
        foreach (var n in notes)
        {
            if (n.Title.Length > 0)
                blocks.Add(new Block { Kind = BlockKind.Heading, Level = 1, Runs = { new Run { Text = n.Title } } });
            blocks.AddRange(n.Blocks);
        }
        return new FormattedNote
        {
            Title = "",
            Blocks = blocks,
            Warnings = notes.SelectMany(n => n.Warnings).Distinct().ToList(),
            Layout = Layout.Document,
            FontSize = notes[0].FontSize
        };
    }
}
