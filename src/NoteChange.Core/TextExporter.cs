using System.Text;
using System.Text.RegularExpressions;

namespace NoteChange.Core;

/// <summary>Plain text and Markdown output.</summary>
public static class TextExporter
{
    public static string ToText(FormattedNote note)
    {
        var parts = new List<string>();
        if (note.Title.Length > 0) parts.Add(note.Title);
        foreach (var b in note.Blocks) parts.Add(Line(b, markdown: false));
        return (string.Join("\n\n", JoinLists(parts, note.Blocks, note.Title.Length > 0)) + "\n").Replace("\n", Environment.NewLine);
    }

    public static string ToMarkdown(FormattedNote note)
    {
        var parts = new List<string>();
        if (note.Title.Length > 0) parts.Add("# " + EscapeMd(note.Title));
        foreach (var b in note.Blocks) parts.Add(Line(b, markdown: true));
        return string.Join("\n\n", JoinLists(parts, note.Blocks, note.Title.Length > 0)) + "\n";
    }

    /// <summary>Keeps list items on consecutive lines instead of separating them with blank lines.</summary>
    private static IEnumerable<string> JoinLists(List<string> parts, List<Block> blocks, bool hasTitle)
    {
        int offset = hasTitle ? 1 : 0;
        var result = new List<string>();
        for (int i = 0; i < parts.Count; i++)
        {
            int bi = i - offset;
            bool isItem = bi >= 0 && blocks[bi].Kind == BlockKind.ListItem;
            bool prevItem = bi - 1 >= 0 && blocks[bi - 1].Kind == BlockKind.ListItem;
            if (isItem && prevItem && result.Count > 0) result[^1] += "\n" + parts[i];
            else result.Add(parts[i]);
        }
        return result;
    }

    public static List<string> MarkdownWarnings(FormattedNote note)
    {
        var w = new List<string>();
        if (note.Blocks.Any(b => b.Runs.Any(r => r.IsImage) || b.Rows.Any(row => row.Any(c => c.Runs.Any(r => r.IsImage)))))
            w.Add("Text and Markdown files can't hold pictures, so each picture becomes a short description. Use Word or PDF to keep them.");
        return w;
    }

    private static string Line(Block b, bool markdown)
    {
        if (b.Kind == BlockKind.Table)
        {
            if (!markdown)
                return string.Join("\n", b.Rows.Select(row => string.Join("\t", row.Select(c => Runs(c.Runs, false).Replace("\n", " ")))));
            var sb = new StringBuilder();
            int cols = b.Rows.Max(r => r.Count);
            for (int r = 0; r < b.Rows.Count; r++)
            {
                var cells = Enumerable.Range(0, cols).Select(c => c < b.Rows[r].Count ? Runs(b.Rows[r][c].Runs, true).Replace("\n", "<br>").Replace("|", "\\|") : "");
                sb.Append("| ").Append(string.Join(" | ", cells)).Append(" |");
                if (r == 0) sb.Append('\n').Append("|").Append(string.Concat(Enumerable.Repeat(" --- |", cols)));
                if (r < b.Rows.Count - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        string text = Runs(b.Runs, markdown);
        switch (b.Kind)
        {
            case BlockKind.Heading:
                return markdown ? new string('#', Math.Clamp(b.Level + 1, 2, 6)) + " " + text.Replace("\n", " ") : text;
            case BlockKind.ListItem:
                string indent = new string(' ', b.Level * (markdown ? 4 : 2));
                string marker = b.Checklist ? (markdown ? "-" : "") : b.Ordered ? b.Number + "." : markdown ? "-" : "•";
                if (b.Checklist && markdown)
                {
                    // ☐ / ☑ become Markdown task boxes.
                    var m = Regex.Match(text, @"^\s*([☐☑])\s*");
                    if (m.Success) text = (m.Groups[1].Value == "☑" ? "[x] " : "[ ] ") + text[m.Length..];
                }
                string body = text.Replace("\n", "\n" + indent + "  ");
                return indent + (marker.Length > 0 ? marker + " " : "") + body;
            default:
                return markdown ? text.Replace("\n", "  \n") : text;
        }
    }

    private static string Runs(IEnumerable<Run> runs, bool markdown)
    {
        var sb = new StringBuilder();
        foreach (var r in runs)
        {
            if (r.Image != null) { sb.Append("[Picture: ").Append(r.Image.Alt).Append(']'); continue; }
            if (!markdown) { sb.Append(r.Text); continue; }

            string t = EscapeMd(r.Text);
            // Keep spaces outside the emphasis markers so Markdown recognises them.
            string lead = t[..(t.Length - t.TrimStart().Length)];
            string trail = t[t.TrimEnd().Length..];
            string core = t.Trim();
            if (core.Length > 0)
            {
                if (r.Strike) core = "~~" + core + "~~";
                if (r.Bold && r.Italic) core = "***" + core + "***";
                else if (r.Bold) core = "**" + core + "**";
                else if (r.Italic) core = "*" + core + "*";
                if (r.Link != null) core = "[" + core + "](" + r.Link.Replace(")", "%29").Replace(" ", "%20") + ")";
            }
            sb.Append(lead).Append(core).Append(trail);
        }
        return sb.ToString();
    }

    private static string EscapeMd(string s) => Regex.Replace(s, @"([\\`*_\[\]<>#])", @"\$1");
}
