using System.Net;
using System.Text;

namespace NoteChange.Core;

/// <summary>Writes formatted notes as HTML for the preview, PDF printing and email.</summary>
public static class HtmlExporter
{
    public static string Escape(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>The note's content (title and body) as an HTML fragment.</summary>
    public static string Fragment(FormattedNote note, bool forEmail = false)
    {
        var sb = new StringBuilder();
        if (note.Title.Length > 0) sb.Append("<h1>").Append(Escape(note.Title)).Append("</h1>");
        AppendBlocks(sb, note.Blocks, forEmail);
        return sb.ToString();
    }

    private static void AppendBlocks(StringBuilder sb, List<Block> blocks, bool forEmail)
    {
        int i = 0;
        while (i < blocks.Count)
        {
            var b = blocks[i];
            if (b.Kind == BlockKind.ListItem)
            {
                int end = i;
                while (end < blocks.Count && blocks[end].Kind == BlockKind.ListItem) end++;
                AppendList(sb, blocks.GetRange(i, end - i), forEmail);
                i = end;
                continue;
            }
            switch (b.Kind)
            {
                case BlockKind.Heading:
                    int level = Math.Clamp(b.Level, 1, 6);
                    // The document title is the only h1, so note headings start at h2.
                    int tag = Math.Min(6, level + 1);
                    sb.Append("<h").Append(tag).Append('>').Append(Inline(b.Runs)).Append("</h").Append(tag).Append('>');
                    break;
                case BlockKind.Table:
                    sb.Append(forEmail ? "<table cellpadding=\"6\" style=\"border-collapse:collapse\">" : "<table>");
                    foreach (var row in b.Rows)
                    {
                        sb.Append("<tr>");
                        foreach (var cell in row)
                        {
                            string t = cell.Header ? "th" : "td";
                            sb.Append('<').Append(t);
                            if (forEmail) sb.Append(" style=\"border:1px solid #c9ced8;text-align:left;vertical-align:top\"");
                            sb.Append('>').Append(Inline(cell.Runs)).Append("</").Append(t).Append('>');
                        }
                        sb.Append("</tr>");
                    }
                    sb.Append("</table>");
                    break;
                default:
                    bool onlyImages = b.Runs.Count > 0 && b.Runs.All(r => r.IsImage);
                    sb.Append(onlyImages ? "<p class=\"figure\">" : "<p>").Append(Inline(b.Runs)).Append("</p>");
                    break;
            }
            i++;
        }
    }

    /// <summary>Writes a run of list items, nesting by level and starting a new list when the kind changes.</summary>
    private static void AppendList(StringBuilder sb, List<Block> items, bool forEmail)
    {
        var open = new Stack<(int Level, string Tag, int Group)>();
        foreach (var item in items)
        {
            string tag = item.Ordered ? "ol" : "ul";
            while (open.Count > 0 && open.Peek().Level > item.Level) sb.Append("</").Append(open.Pop().Tag).Append('>');
            if (open.Count > 0 && open.Peek().Level == item.Level && (open.Peek().Tag != tag || open.Peek().Group != item.Group))
                sb.Append("</").Append(open.Pop().Tag).Append('>');
            while (open.Count == 0 || open.Peek().Level < item.Level)
            {
                int level = open.Count == 0 ? item.Level : open.Peek().Level + 1;
                bool here = level == item.Level;
                sb.Append('<').Append(here ? tag : "ul");
                if (here && item.Checklist) sb.Append(forEmail ? " style=\"list-style:none;padding-left:0\"" : " class=\"checklist\"");
                if (here && item.Ordered && item.Number > 1) sb.Append(" start=\"").Append(item.Number).Append('"');
                sb.Append('>');
                open.Push((level, here ? tag : "ul", here ? item.Group : -1));
            }
            sb.Append("<li>").Append(Inline(item.Runs)).Append("</li>");
        }
        while (open.Count > 0) sb.Append("</").Append(open.Pop().Tag).Append('>');
    }

    public static string Inline(IEnumerable<Run> runs)
    {
        var sb = new StringBuilder();
        foreach (var r in runs)
        {
            if (r.Image != null)
            {
                sb.Append("<img src=\"").Append(r.Image.DataUri).Append("\" alt=\"").Append(Escape(r.Image.Alt))
                  .Append("\" width=\"").Append(r.Image.Width).Append("\" height=\"").Append(r.Image.Height).Append("\">");
                continue;
            }
            string t = Escape(r.Text).Replace("\n", "<br>");
            if (r.Bold) t = "<strong>" + t + "</strong>";
            if (r.Italic) t = "<em>" + t + "</em>";
            if (r.Underline) t = "<u>" + t + "</u>";
            if (r.Strike) t = "<s>" + t + "</s>";
            if (r.Link != null) t = "<a href=\"" + Escape(r.Link) + "\">" + t + "</a>";
            sb.Append(t);
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- whole pages

    private const string PaperCss = @"
.page{background:#fff;color:#1d2330;font-family:Calibri,Carlito,'Segoe UI',Arial,sans-serif;line-height:1.55}
.page h1{font-size:2em;line-height:1.2;margin:0 0 .7em;font-weight:700;letter-spacing:-.01em}
.page h2{font-size:1.35em;margin:1.4em 0 .5em;line-height:1.25}
.page h3{font-size:1.15em;margin:1.2em 0 .4em}
.page h4,.page h5,.page h6{font-size:1em;margin:1em 0 .3em}
.page p{margin:0 0 .8em;overflow-wrap:anywhere}
.page p.figure{text-align:center}
.page img{max-width:100%;height:auto;border-radius:4px}
.page ul,.page ol{margin:0 0 .8em;padding-left:1.6em}
.page li{margin:.2em 0}
.page ul.checklist{list-style:none;padding-left:.2em}
.page a{color:#1f5fbf}
.page table{width:100%;border-collapse:collapse;margin:.4em 0 1em}
.page td,.page th{border:1px solid #c9ced8;padding:.4em .6em;text-align:left;vertical-align:top}
.page th{background:#f2f3f6}
.page.layout-recipe h1{padding-bottom:.45em;border-bottom:3px solid #d98a1c}
.page.layout-recipe h2{color:#a55f00;font-size:.95em;text-transform:uppercase;letter-spacing:.09em;margin-top:1.6em}
.page.layout-recipe ol li{margin:.45em 0}
.page.layout-chapter{font-family:Georgia,'Times New Roman',serif;line-height:1.8}
.page.layout-chapter h1{text-align:center;font-weight:400;font-size:1.9em;margin-bottom:1.2em}
.page.layout-chapter h1::after{content:'';display:block;width:56px;height:1px;background:#9aa1ad;margin:.7em auto 0}
.page.layout-chapter p{text-indent:1.5em;margin:0;text-align:justify;hyphens:auto}
.page.layout-chapter h1+p,.page.layout-chapter h2+p,.page.layout-chapter p.figure{text-indent:0}
.page.layout-chapter h2{text-align:center;font-weight:400;font-style:italic}
";

    /// <summary>The live preview: paper pages on a dark desk.</summary>
    public static string PreviewPage(IReadOnlyList<FormattedNote> notes, string? heading)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"en-GB\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:\">");
        sb.Append("<style>");
        sb.Append("html{background:#0b0d11}body{margin:0;padding:28px 24px 64px;font:14px 'Segoe UI',sans-serif;color:#a7abb5}");
        sb.Append(".label{max-width:794px;margin:0 auto 14px;font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:#7d828d}");
        sb.Append(".page{max-width:794px;box-sizing:border-box;margin:0 auto 28px;padding:72px 76px 80px;border-radius:6px;box-shadow:0 18px 50px rgba(0,0,0,.45),0 0 0 1px rgba(255,255,255,.04)}");
        sb.Append("@media (max-width:700px){.page{padding:40px 32px}}");
        sb.Append("::-webkit-scrollbar{width:12px}::-webkit-scrollbar-track{background:#0b0d11}::-webkit-scrollbar-thumb{background:rgba(255,255,255,.16);border-radius:6px;border:3px solid #0b0d11}::-webkit-scrollbar-thumb:hover{background:rgba(255,255,255,.28)}");
        sb.Append(PaperCss);
        sb.Append("</style></head><body>");
        if (!string.IsNullOrEmpty(heading)) sb.Append("<div class=\"label\">").Append(Escape(heading)).Append("</div>");
        foreach (var n in notes)
        {
            sb.Append("<article class=\"page layout-").Append(n.Layout.ToString().ToLowerInvariant())
              .Append("\" style=\"font-size:").Append(n.FontSize).Append("pt\">")
              .Append(Fragment(n)).Append("</article>");
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>A print-ready page for PDF export: A4, one note per page run.</summary>
    public static string PrintPage(IReadOnlyList<FormattedNote> notes)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"en-GB\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:\">");
        sb.Append("<style>@page{size:A4}html,body{margin:0;background:#fff}");
        sb.Append(PaperCss);
        sb.Append(".page+.page{break-before:page}h1,h2,h3{break-after:avoid}img,tr{break-inside:avoid}");
        sb.Append("</style></head><body>");
        foreach (var n in notes)
        {
            sb.Append("<article class=\"page layout-").Append(n.Layout.ToString().ToLowerInvariant())
              .Append("\" style=\"font-size:").Append(n.FontSize).Append("pt\">")
              .Append(Fragment(n)).Append("</article>");
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>Simple, inline-styled HTML that email programs display well.</summary>
    public static string EmailHtml(FormattedNote note)
    {
        string font = note.Layout == Layout.Chapter ? "Georgia,serif" : "Calibri,Arial,sans-serif";
        return "<div style=\"font-family:" + font + ";font-size:" + Math.Min(note.FontSize, 12) + "pt;line-height:1.5;color:#1d2330\">"
            + Fragment(note, forEmail: true) + "</div>";
    }
}
