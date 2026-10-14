using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace NoteChange.Core;

/// <summary>
/// Turns a note's HTML (or plain text) into clean blocks. The HTML is never shown as-is: only text,
/// basic emphasis, links, lists, tables and PNG/JPEG pictures are read out of it, so nothing
/// from the source page can run or load anything.
/// </summary>
public static class Importer
{
    public const int MaxChars = 12 * 1024 * 1024;

    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "head", "title", "meta", "link", "noscript", "template", "svg", "iframe",
        "object", "embed", "video", "audio", "canvas", "select", "option", "textarea", "button", "math"
    };

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "table", "blockquote", "pre",
        "section", "article", "header", "footer", "main", "aside", "nav", "figure", "figcaption", "center",
        "address", "dl", "dt", "dd", "hr", "body", "form", "fieldset", "details", "summary"
    };

    private static readonly Regex Whitespace = new(@"[ \t\r\n\f]+", RegexOptions.Compiled);
    private static readonly Regex BulletLine = new(@"^\s*([-*•‣◦▪])\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex NumberLine = new(@"^\s*(\d{1,3})[.)]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex CheckLine = new(@"^\s*([☐☑✓✔]|\[[ xX]\])\s*(.*)$", RegexOptions.Compiled);

    public static ImportedNote Import(NoteSource source)
    {
        if (source == null || (string.IsNullOrWhiteSpace(source.Text) && string.IsNullOrWhiteSpace(source.Html)))
            throw new NoteException("This note is empty. Choose a note or paste its contents.");
        if ((source.Text?.Length ?? 0) + (source.Html?.Length ?? 0) > MaxChars)
            throw new NoteException("This note is too large. Please use a smaller note (up to 12 MB).");

        var state = new State();
        state.Warnings.AddRange(source.Warnings.Where(w => !string.IsNullOrWhiteSpace(w)));

        if (!string.IsNullOrWhiteSpace(source.Html))
        {
            var document = new HtmlParser().ParseDocument("<!doctype html><html><body>" + source.Html + "</body></html>");
            if (document.Body != null) Walk(document.Body, state, 0, new Style());
        }
        else
        {
            FromPlainText(source.Text ?? "", state);
        }

        var blocks = state.Blocks.Where(b => b.HasContent).ToList();
        foreach (var b in blocks) Tidy(b.Runs);
        blocks = blocks.Where(b => b.HasContent).ToList();
        if (blocks.Count == 0)
            throw new NoteException("This note is empty. Choose a note or paste its contents.");

        var first = blocks.FirstOrDefault(b => b.Kind != BlockKind.Table && b.Runs.Any(r => r.HasText));
        string title = first == null ? "" : first.PlainText.Split('\n')[0].Trim();

        return new ImportedNote { Blocks = blocks, Warnings = state.Warnings.Distinct().ToList(), Title = title };
    }

    // ---------------------------------------------------------------- plain text

    private static void FromPlainText(string text, State state)
    {
        Block? lastList = null;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.Trim().Length == 0) { lastList = null; continue; }

            Match m;
            if ((m = CheckLine.Match(line)).Success && m.Groups[2].Value.Length > 0)
            {
                bool done = m.Groups[1].Value is "☑" or "✓" or "✔" or "[x]" or "[X]";
                lastList = AddListLine(state, lastList, (done ? "☑ " : "☐ ") + m.Groups[2].Value, false, true, 0);
            }
            else if ((m = BulletLine.Match(line)).Success && m.Groups[2].Value.Length > 0)
            {
                int level = Math.Min(8, (line.Length - line.TrimStart().Length) / 2);
                lastList = AddListLine(state, lastList, m.Groups[2].Value, false, false, level);
            }
            else if ((m = NumberLine.Match(line)).Success && m.Groups[2].Value.Length > 0)
            {
                int level = Math.Min(8, (line.Length - line.TrimStart().Length) / 2);
                lastList = AddListLine(state, lastList, m.Groups[2].Value, true, false, level);
            }
            else
            {
                lastList = null;
                state.Blocks.Add(new Block { Kind = BlockKind.Paragraph, Runs = { new Run { Text = line.Trim() } } });
            }
        }
    }

    private static Block AddListLine(State state, Block? previous, string text, bool ordered, bool checklist, int level)
    {
        bool continues = previous != null && previous.Ordered == ordered && previous.Checklist == checklist;
        int group = continues ? previous!.Group : state.NextGroup++;
        int number = 1;
        if (continues)
        {
            var sameLevel = state.Blocks.LastOrDefault(b => b.Kind == BlockKind.ListItem && b.Group == group && b.Level == level);
            number = sameLevel == null ? 1 : sameLevel.Number + 1;
        }
        var block = new Block
        {
            Kind = BlockKind.ListItem, Ordered = ordered, Checklist = checklist, Level = level,
            Group = group, Number = number, Runs = { new Run { Text = text.Trim() } }
        };
        state.Blocks.Add(block);
        return block;
    }

    // ---------------------------------------------------------------- HTML

    private sealed class State
    {
        public List<Block> Blocks { get; } = new();
        public List<string> Warnings { get; } = new();
        public int NextGroup;
    }

    private readonly record struct Style(bool Bold = false, bool Italic = false, bool Underline = false, bool Strike = false, string? Link = null, bool Pre = false);

    private static bool IsBlock(IElement e) => BlockTags.Contains(e.LocalName);

    private static bool HasBlockInside(IElement e) => e.Descendants<IElement>().Any(IsBlock);

    /// <summary>Walks a container, turning runs of inline content into paragraphs and handling block elements.</summary>
    private static void Walk(INode parent, State state, int listLevel, Style style)
    {
        var inline = new List<INode>();

        void Flush()
        {
            if (inline.Count == 0) return;
            var runs = new List<Run>();
            foreach (var n in inline) runs.AddRange(Runs(n, style, state));
            inline.Clear();
            AddParagraph(state, runs);
        }

        foreach (var node in parent.ChildNodes)
        {
            if (node is IElement el)
            {
                if (Ignored.Contains(el.LocalName)) continue;
                if (IsBlock(el))
                {
                    Flush();
                    HandleBlock(el, state, listLevel, StyleOf(el, style));
                }
                else if (HasBlockInside(el))
                {
                    // An inline wrapper around blocks, such as <b><div>…</div></b>: keep its emphasis.
                    Flush();
                    Walk(el, state, listLevel, StyleOf(el, style));
                }
                else inline.Add(el);
            }
            else if (node.NodeType == NodeType.Text) inline.Add(node);
        }
        Flush();
    }

    private static void HandleBlock(IElement el, State state, int listLevel, Style style)
    {
        string tag = el.LocalName.ToLowerInvariant();
        switch (tag)
        {
            case "hr":
                return;
            case "ul":
            case "ol":
                AddList(el, state, listLevel, style);
                return;
            case "table":
                AddTable(el, state, style);
                return;
            case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                if (HasBlockInside(el)) { Walk(el, state, listLevel, style); return; }
                var runs = ChildRuns(el, style, state);
                if (runs.Any(r => r.HasText))
                    state.Blocks.Add(new Block { Kind = BlockKind.Heading, Level = tag[1] - '0', Runs = runs });
                else AddParagraph(state, runs);
                return;
            case "pre":
                AddParagraph(state, ChildRuns(el, style with { Pre = true }, state));
                return;
            default:
                if (HasBlockInside(el)) Walk(el, state, listLevel, style);
                else AddParagraph(state, ChildRuns(el, style, state));
                return;
        }
    }

    private static void AddParagraph(State state, List<Run> runs)
    {
        var block = new Block { Kind = BlockKind.Paragraph, Runs = runs };
        Tidy(block.Runs);
        if (block.HasContent) state.Blocks.Add(block);
    }

    private static void AddList(IElement list, State state, int level, Style style)
    {
        bool ordered = list.LocalName.Equals("ol", StringComparison.OrdinalIgnoreCase);
        int group = state.NextGroup++;
        int number = 1;
        if (ordered && int.TryParse(list.GetAttribute("start"), out int start) && start > 0) number = start;

        foreach (var child in list.Children)
        {
            string tag = child.LocalName.ToLowerInvariant();
            if (tag is "ul" or "ol")
            {
                AddList(child, state, Math.Min(level + 1, 8), style);
                continue;
            }
            if (tag != "li") continue;

            var runs = new List<Run>();
            bool afterBlock = false;
            foreach (var n in child.ChildNodes)
            {
                if (n is IElement e && (e.LocalName is "ul" or "ol" || Ignored.Contains(e.LocalName))) continue;
                bool block = n is IElement be && IsBlock(be);
                var part = Runs(n, StyleOf(child, style), state);
                if ((block || afterBlock) && runs.Count > 0 && part.Any(r => r.IsImage || r.Text.Length > 0)) runs.Add(new Run { Text = "\n" });
                runs.AddRange(part);
                afterBlock = block;
            }
            Tidy(runs);

            var item = new Block { Kind = BlockKind.ListItem, Ordered = ordered, Level = level, Group = group, Runs = runs };
            string text = item.PlainText.TrimStart();
            if (text.StartsWith('☐') || text.StartsWith('☑')) { item.Checklist = true; item.Ordered = false; }
            if (item.HasContent) { item.Number = number++; state.Blocks.Add(item); }

            foreach (var nested in child.Children.Where(c => c.LocalName is "ul" or "ol"))
                AddList(nested, state, Math.Min(level + 1, 8), style);
        }
    }

    private static void AddTable(IElement table, State state, Style style)
    {
        var rows = new List<List<TableCell>>();
        foreach (var tr in table.QuerySelectorAll("tr"))
        {
            if (tr.Closest("table") != table) continue;
            var cells = new List<TableCell>();
            foreach (var cell in tr.Children.Where(c => c.LocalName is "td" or "th"))
            {
                var runs = ChildRuns(cell, style, state);
                Tidy(runs);
                cells.Add(new TableCell { Header = cell.LocalName == "th", Runs = runs });
            }
            if (cells.Count > 0) rows.Add(cells);
        }
        if (rows.Count == 0) return;
        if (table.QuerySelector("[colspan],[rowspan]") != null)
            state.Warnings.Add("Merged table cells are simplified. Please check the table in the preview.");
        state.Blocks.Add(new Block { Kind = BlockKind.Table, Rows = rows });
    }

    private static List<Run> ChildRuns(IElement el, Style style, State state)
    {
        var runs = new List<Run>();
        bool afterBlock = false;
        foreach (var n in el.ChildNodes)
        {
            bool block = n is IElement e && IsBlock(e);
            var part = Runs(n, style, state);
            if ((block || afterBlock) && runs.Count > 0 && part.Count > 0) runs.Add(new Run { Text = "\n" });
            runs.AddRange(part);
            afterBlock = block;
        }
        return runs;
    }

    private static List<Run> Runs(INode node, Style style, State state)
    {
        var list = new List<Run>();
        if (node.NodeType == NodeType.Text)
        {
            string text = node.TextContent;
            if (!style.Pre) text = Whitespace.Replace(text, " ");
            text = text.Replace(' ', ' ');
            if (text.Length > 0)
                list.Add(new Run { Text = text, Bold = style.Bold, Italic = style.Italic, Underline = style.Underline, Strike = style.Strike, Link = style.Link });
            return list;
        }
        if (node is not IElement el) return list;

        string tag = el.LocalName.ToLowerInvariant();
        if (Ignored.Contains(tag)) return list;

        if (tag == "br") { list.Add(new Run { Text = "\n" }); return list; }

        if (tag == "img")
        {
            var image = ImageInfo.FromDataUri(el.GetAttribute("src"), el.GetAttribute("alt"));
            if (image != null) list.Add(new Run { Image = image });
            else
            {
                state.Warnings.Add("A picture couldn't be brought across. Check the preview, and save that picture separately if you need it.");
                string alt = el.GetAttribute("alt") ?? "";
                list.Add(new Run { Text = "[Picture" + (alt.Trim().Length > 0 ? ": " + alt.Trim() : "") + "]", Italic = true });
            }
            return list;
        }

        if (tag == "input")
        {
            if (string.Equals(el.GetAttribute("type"), "checkbox", StringComparison.OrdinalIgnoreCase))
                list.Add(new Run { Text = el.HasAttribute("checked") ? "☑ " : "☐ " });
            return list;
        }

        var inner = StyleOf(el, style);
        bool afterBlock = false;
        foreach (var child in el.ChildNodes)
        {
            bool block = child is IElement ce && IsBlock(ce);
            var part = Runs(child, inner, state);
            if ((block || afterBlock) && list.Count > 0 && part.Count > 0) list.Add(new Run { Text = "\n" });
            list.AddRange(part);
            afterBlock = block;
        }
        return list;
    }

    private static Style StyleOf(IElement el, Style style)
    {
        string tag = el.LocalName.ToLowerInvariant();
        var s = style;
        if (tag is "b" or "strong") s = s with { Bold = true };
        if (tag is "i" or "em" or "cite" or "var") s = s with { Italic = true };
        if (tag is "u" or "ins") s = s with { Underline = true };
        if (tag is "s" or "strike" or "del") s = s with { Strike = true };
        if (tag is "pre" or "code") s = s with { Pre = tag == "pre" || s.Pre };
        if (tag == "a")
        {
            string? href = el.GetAttribute("href")?.Trim();
            if (SafeLink(href)) s = s with { Link = href };
        }

        string css = el.GetAttribute("style") ?? "";
        if (css.Length > 0)
        {
            var weight = Regex.Match(css, @"font-weight\s*:\s*([a-z0-9]+)", RegexOptions.IgnoreCase);
            if (weight.Success)
            {
                string w = weight.Groups[1].Value.ToLowerInvariant();
                if (w is "bold" or "bolder" || (int.TryParse(w, out int n) && n >= 600)) s = s with { Bold = true };
                else if (w is "normal" or "lighter" || (int.TryParse(w, out int m) && m < 600)) s = s with { Bold = false };
            }
            var fontStyle = Regex.Match(css, @"font-style\s*:\s*([a-z]+)", RegexOptions.IgnoreCase);
            if (fontStyle.Success) s = s with { Italic = fontStyle.Groups[1].Value.Equals("italic", StringComparison.OrdinalIgnoreCase) || fontStyle.Groups[1].Value.Equals("oblique", StringComparison.OrdinalIgnoreCase) };
            var decoration = Regex.Match(css, @"text-decoration(?:-line)?\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
            if (decoration.Success)
            {
                string d = decoration.Groups[1].Value.ToLowerInvariant();
                if (d.Contains("underline")) s = s with { Underline = true };
                if (d.Contains("line-through")) s = s with { Strike = true };
            }
            if (Regex.IsMatch(css, @"white-space\s*:\s*pre", RegexOptions.IgnoreCase)) s = s with { Pre = true };
        }
        return s;
    }

    public static bool SafeLink(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return false;
        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme is "https" or "http" or "mailto";
    }

    /// <summary>Trims the edges of a paragraph, tidies spaces around line breaks and merges runs with the same style.</summary>
    internal static void Tidy(List<Run> runs)
    {
        // Merge neighbours with the same formatting.
        for (int i = runs.Count - 1; i > 0; i--)
        {
            if (runs[i].SameStyle(runs[i - 1]))
            {
                runs[i - 1].Text += runs[i].Text;
                runs.RemoveAt(i);
            }
        }
        foreach (var r in runs.Where(r => !r.IsImage))
        {
            r.Text = Regex.Replace(r.Text, @" *\n *", "\n");
            r.Text = Regex.Replace(r.Text, @"\n{3,}", "\n\n");
        }
        // Trim the start and end of the whole paragraph.
        while (runs.Count > 0 && !runs[0].IsImage)
        {
            runs[0].Text = runs[0].Text.TrimStart(' ', '\n');
            if (runs[0].Text.Length == 0) runs.RemoveAt(0); else break;
        }
        while (runs.Count > 0 && !runs[^1].IsImage)
        {
            runs[^1].Text = runs[^1].Text.TrimEnd(' ', '\n');
            if (runs[^1].Text.Length == 0) runs.RemoveAt(runs.Count - 1); else break;
        }
    }
}
