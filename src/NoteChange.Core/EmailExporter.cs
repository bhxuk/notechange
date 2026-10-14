using System.Text;
using System.Text.RegularExpressions;

namespace NoteChange.Core;

/// <summary>
/// Writes an unsent email draft (.eml) with a plain text part, an HTML part and inline pictures.
/// Outlook, Mail and Thunderbird open it as a new message ready for a recipient. The subject is left
/// for the person to write.
/// </summary>
public static class EmailExporter
{
    public static string ToEml(FormattedNote note)
    {
        string alternative = "nc-alt-" + Guid.NewGuid().ToString("N")[..12];
        string related = "nc-rel-" + Guid.NewGuid().ToString("N")[..12];
        var images = new List<(string Cid, string Type, string Base64)>();

        string html = "<!doctype html><html><head><meta charset=\"utf-8\"></head><body>" + HtmlExporter.EmailHtml(note) + "</body></html>";
        html = Regex.Replace(html, "src=\"data:image/(png|jpeg);base64,([^\"]+)\"", m =>
        {
            string cid = "picture" + images.Count + "@notechange";
            images.Add((cid, m.Groups[1].Value, m.Groups[2].Value));
            return "src=\"cid:" + cid + "\"";
        });

        var sb = new StringBuilder();
        void Line(string s = "") => sb.Append(s).Append("\r\n");

        Line("X-Unsent: 1");
        Line("Subject: ");
        Line("MIME-Version: 1.0");
        Line("Content-Type: multipart/related; boundary=\"" + related + "\"");
        Line();
        Line("--" + related);
        Line("Content-Type: multipart/alternative; boundary=\"" + alternative + "\"");
        Line();
        Line("--" + alternative);
        Line("Content-Type: text/plain; charset=UTF-8");
        Line("Content-Transfer-Encoding: base64");
        Line();
        Line(Wrap(Convert.ToBase64String(Encoding.UTF8.GetBytes(TextExporter.ToText(note)))));
        Line("--" + alternative);
        Line("Content-Type: text/html; charset=UTF-8");
        Line("Content-Transfer-Encoding: base64");
        Line();
        Line(Wrap(Convert.ToBase64String(Encoding.UTF8.GetBytes(html))));
        Line("--" + alternative + "--");
        foreach (var img in images)
        {
            Line("--" + related);
            Line("Content-Type: image/" + img.Type);
            Line("Content-ID: <" + img.Cid + ">");
            Line("Content-Disposition: inline");
            Line("Content-Transfer-Encoding: base64");
            Line();
            Line(Wrap(img.Base64));
        }
        Line("--" + related + "--");
        return sb.ToString();
    }

    private static string Wrap(string base64)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < base64.Length; i += 76)
        {
            if (i > 0) sb.Append("\r\n");
            sb.Append(base64, i, Math.Min(76, base64.Length - i));
        }
        return sb.ToString();
    }

    /// <summary>A mailto: link with the note as the body, or null if it's too long for a link.</summary>
    public static string? MailtoLink(FormattedNote note)
    {
        string body = Uri.EscapeDataString(TextExporter.ToText(note).Replace(Environment.NewLine, "\r\n"));
        return body.Length > 1800 ? null : "mailto:?body=" + body;
    }
}
