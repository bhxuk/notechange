using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using NoteChange.Core;

namespace NoteChange
{
    /// <summary>
    /// Finds the open note on the iCloud page and reads it. Three ways, in order:
    /// 1. The browser's inspector view of the whole page, which reaches nested frames and hidden (closed)
    ///    parts of the page, then reads the editor element it finds.
    /// 2. A search run inside each frame.
    /// 3. Whatever the person has selected in the note.
    /// </summary>
    public static class CaptureRunner
    {
        public sealed class Result
        {
            public NoteSource Source;
            public string Error;
            /// <summary>A description of the page's structure (never its text), for troubleshooting.</summary>
            public string Details;
        }

        private sealed class Piece
        {
            public bool Visible;
            public int W, H;
            public bool Title;
            public string Html = "";
            public string Text = "";
            public List<string> Warnings = new List<string>();
            public bool FromSelection;
            public long Area => (long)W * H;
        }

        private static string _lib;

        private static string Lib
        {
            get
            {
                if (_lib != null) return _lib;
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NoteChange.Capture.js");
                using var reader = new StreamReader(stream);
                return _lib = reader.ReadToEnd();
            }
        }

        public const string NotFound =
            "NoteChange couldn't find the note on this page. Click inside the note, press Ctrl+A to select all of it, then press Add this note again.";

        public static async Task<Result> Run(CoreWebView2 core, IList<CoreWebView2Frame> frames)
        {
            var details = new StringBuilder();
            details.AppendLine("NoteChange " + Updates.CurrentText + " capture details (no note text included)");
            details.AppendLine("WebView2 " + Browser.RuntimeVersion());
            var pieces = new List<Piece>();

            // 1. The inspector view of the whole page.
            try
            {
                pieces.AddRange(await FromInspector(core, details));
            }
            catch (Exception ex)
            {
                details.AppendLine("Inspector: failed: " + ex.GetType().Name + ": " + ex.Message);
                Log.Write("Capture (inspector) failed: " + ex.Message);
            }

            // 2. A search inside each frame, which also picks up a selection.
            Piece selection = null;
            if (!pieces.Any(HasContent))
            {
                var (found, selected) = await FromFrames(core, frames, details);
                pieces.AddRange(found);
                selection = selected;
            }

            var choice = Choose(pieces.Where(HasContent).ToList(), out string ambiguous);
            if (choice == null && ambiguous == null && selection != null && HasContent(selection)) choice = selection;
            details.AppendLine("Result: " + (choice != null ? (choice.FromSelection ? "selection" : "editor " + choice.W + "x" + choice.H) : ambiguous ?? "nothing found"));

            if (choice == null)
                return new Result { Error = ambiguous ?? NotFound, Details = details.ToString() };

            var source = NoteSource.FromHtml(choice.Html ?? "", choice.Text ?? "", "icloud");
            source.Warnings.AddRange(choice.Warnings.Distinct());
            return new Result { Source = source, Details = details.ToString() };
        }

        private static bool HasContent(Piece p) =>
            p != null && p.Visible && (!string.IsNullOrWhiteSpace(p.Text) || (p.Html ?? "").IndexOf("<img", StringComparison.OrdinalIgnoreCase) >= 0);

        private static string Normalise(string s) => Regex.Replace(s ?? "", @"\s+", " ").Trim();

        /// <summary>Picks the note body (and a separate title, if the editor splits them).</summary>
        private static Piece Choose(List<Piece> pieces, out string ambiguous)
        {
            ambiguous = null;
            // The same editor can be found more than once.
            pieces = pieces.GroupBy(p => Normalise(p.Text) + "|" + p.Html.Length).Select(g => g.First()).ToList();
            if (pieces.Count == 0) return null;
            if (pieces.Count == 1) return pieces[0];

            var titles = pieces.Where(p => p.Title).ToList();
            var bodies = pieces.Where(p => !p.Title).OrderByDescending(p => p.Area).ToList();
            if (bodies.Count == 0) { bodies = titles.OrderByDescending(p => p.Area).ToList(); titles.Clear(); }

            Piece body = bodies.Count == 1 || bodies[0].Area >= 2 * bodies[1].Area ? bodies[0] : null;
            if (body == null)
            {
                ambiguous = "More than one note seems to be open. Click inside the note you want, press Ctrl+A to select it, then press Add this note.";
                return null;
            }
            if (titles.Count != 1) return body;

            var t = titles[0];
            return new Piece
            {
                Visible = true, W = body.W, H = body.H,
                Html = "<h1>" + t.Html + "</h1>" + body.Html,
                Text = t.Text + "\n" + body.Text,
                Warnings = t.Warnings.Concat(body.Warnings).ToList()
            };
        }

        // ------------------------------------------------------------ 1. inspector

        private sealed class Candidate
        {
            public long BackendId;
            public string Describe;
        }

        private sealed class Stats
        {
            public int Elements, Frames, ShadowOpen, ShadowClosed, Editable, Textbox, EditableFalse;
            public readonly List<string> Documents = new List<string>();
            public readonly Dictionary<string, int> Custom = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Roles = new Dictionary<string, int>();
        }

        private static async Task<List<Piece>> FromInspector(CoreWebView2 core, StringBuilder details)
        {
            var pieces = new List<Piece>();
            string json = await Cdp(core, "DOM.getDocument", new { depth = -1, pierce = true });
            using var doc = JsonDocument.Parse(json);
            var stats = new Stats();
            var candidates = new List<Candidate>();
            Walk(doc.RootElement.GetProperty("root"), false, false, stats, candidates);

            details.AppendLine("Pages: " + string.Join(" | ", stats.Documents.Take(12)));
            details.AppendLine("Elements " + stats.Elements + ", frames " + stats.Frames + ", shadow roots open " + stats.ShadowOpen + " closed " + stats.ShadowClosed +
                               ", editable " + stats.Editable + ", textbox " + stats.Textbox + ", editable=false " + stats.EditableFalse);
            if (stats.Roles.Count > 0)
                details.AppendLine("Roles: " + string.Join(", ", stats.Roles.OrderByDescending(r => r.Value).Take(15).Select(r => r.Key + " " + r.Value)));
            if (stats.Custom.Count > 0)
                details.AppendLine("Custom elements: " + string.Join(", ", stats.Custom.OrderByDescending(r => r.Value).Take(15).Select(r => r.Key + " " + r.Value)));

            string function = "async function(){\n" + Lib + "\nreturn await ncCaptureElement(this);\n}";
            foreach (var c in candidates.Take(30))
            {
                try
                {
                    string resolved = await Cdp(core, "DOM.resolveNode", new { backendNodeId = c.BackendId });
                    string objectId;
                    using (var r = JsonDocument.Parse(resolved)) objectId = r.RootElement.GetProperty("object").GetProperty("objectId").GetString();
                    string called = await Cdp(core, "Runtime.callFunctionOn", new { functionDeclaration = function, objectId, awaitPromise = true, returnByValue = true });
                    try { await Cdp(core, "Runtime.releaseObject", new { objectId }); } catch { }
                    using var result = JsonDocument.Parse(called);
                    if (result.RootElement.TryGetProperty("exceptionDetails", out var ex))
                    {
                        details.AppendLine("Candidate " + c.Describe + ": script error " + Short(ex.ToString()));
                        continue;
                    }
                    var value = result.RootElement.GetProperty("result").GetProperty("value");
                    var piece = ToPiece(value);
                    details.AppendLine("Candidate " + c.Describe + ": " + (piece.Visible ? "visible " + piece.W + "x" + piece.H + (piece.Title ? " title" : "") + ", " + piece.Text.Length + " chars" : "hidden " + piece.W + "x" + piece.H));
                    pieces.Add(piece);
                }
                catch (Exception ex)
                {
                    details.AppendLine("Candidate " + c.Describe + ": failed " + ex.Message);
                }
            }
            return pieces;
        }

        private static void Walk(JsonElement node, bool allowed, bool insideCandidate, Stats stats, List<Candidate> candidates)
        {
            int type = node.TryGetProperty("nodeType", out var t) ? t.GetInt32() : 0;

            if (type == 9 && node.TryGetProperty("documentURL", out var urlProp))
            {
                string url = urlProp.GetString() ?? "";
                stats.Frames++;
                if (Uri.TryCreate(url, UriKind.Absolute, out var u))
                {
                    if (u.Scheme == "https") allowed = u.Host.Equals("icloud.com", StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith(".icloud.com", StringComparison.OrdinalIgnoreCase);
                    else if (u.Scheme != "about" && u.Scheme != "blob" && u.Scheme != "data") allowed = false;
                    stats.Documents.Add(u.Scheme == "https" ? u.Host + u.AbsolutePath : u.Scheme + ":" + (u.Scheme == "about" ? u.AbsolutePath : ""));
                }
            }

            bool isCandidate = false;
            if (type == 1)
            {
                stats.Elements++;
                string tag = node.TryGetProperty("localName", out var ln) ? ln.GetString() ?? "" : "";
                if (tag.Contains('-')) stats.Custom[tag] = stats.Custom.TryGetValue(tag, out int n) ? n + 1 : 1;

                string editable = null, role = null, cls = null, id = null;
                if (node.TryGetProperty("attributes", out var attrs))
                {
                    for (int i = 0; i + 1 < attrs.GetArrayLength(); i += 2)
                    {
                        string name = attrs[i].GetString(), value = attrs[i + 1].GetString();
                        switch (name)
                        {
                            case "contenteditable": editable = value ?? ""; break;
                            case "role": role = value; break;
                            case "class": cls = value; break;
                            case "id": id = value; break;
                        }
                    }
                }
                if (role != null) stats.Roles[role] = stats.Roles.TryGetValue(role, out int rn) ? rn + 1 : 1;
                if (editable != null && editable.Equals("false", StringComparison.OrdinalIgnoreCase)) stats.EditableFalse++;
                else if (editable != null) { stats.Editable++; isCandidate = true; }
                if (role == "textbox") { stats.Textbox++; isCandidate = true; }

                if (isCandidate && allowed && !insideCandidate && node.TryGetProperty("backendNodeId", out var bid))
                {
                    string describe = tag + (string.IsNullOrEmpty(id) ? "" : "#" + id) +
                        (string.IsNullOrWhiteSpace(cls) ? "" : "." + string.Join(".", cls.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4))) +
                        (role != null ? "[role=" + role + "]" : "") + (editable != null ? "[contenteditable=" + editable + "]" : "");
                    candidates.Add(new Candidate { BackendId = bid.GetInt64(), Describe = describe });
                }
            }

            bool inner = insideCandidate || isCandidate;
            if (node.TryGetProperty("shadowRoots", out var shadows))
            {
                foreach (var sr in shadows.EnumerateArray())
                {
                    string kind = sr.TryGetProperty("shadowRootType", out var k) ? k.GetString() : "open";
                    if (kind == "user-agent") continue;
                    if (kind == "closed") stats.ShadowClosed++; else stats.ShadowOpen++;
                    Walk(sr, allowed, inner, stats, candidates);
                }
            }
            if (node.TryGetProperty("contentDocument", out var content)) Walk(content, allowed, false, stats, candidates);
            if (node.TryGetProperty("children", out var children))
                foreach (var child in children.EnumerateArray()) Walk(child, allowed, inner, stats, candidates);
        }

        // ------------------------------------------------------------ 2. inside each frame

        private static async Task<(List<Piece>, Piece)> FromFrames(CoreWebView2 core, IList<CoreWebView2Frame> frames, StringBuilder details)
        {
            var pieces = new List<Piece>();
            Piece selection = null;
            string token = "__notechange_" + Guid.NewGuid().ToString("N");
            string key = JsonSerializer.Serialize(token);
            string script = "(function(){var key=" + key + ";window[key]={done:false};(async function(){\n" + Lib +
                            "\nreturn await ncScanDocument();\n})().then(function(r){window[key]={done:true,result:r};},function(e){window[key]={done:true,result:{error:String((e&&e.message)||e)}};});return 'started';})()";
            string poll = "(function(){var n=window[" + key + "];return (n&&n.done)?JSON.stringify(n.result):null;})()";

            var runners = new List<(string Name, Func<string, Task<string>> Run)> { ("page", s => core.ExecuteScriptAsync(s)) };
            int index = 0;
            foreach (var f in frames.ToList())
            {
                var frame = f;
                string name;
                try { name = "frame " + (++index) + " " + frame.Name; } catch { name = "frame " + index; }
                runners.Add((name, s => frame.ExecuteScriptAsync(s)));
            }

            var pending = new List<(string Name, Func<string, Task<string>> Run)>();
            foreach (var r in runners)
            {
                try
                {
                    string started = await WithTimeout(r.Run(script), 10);
                    if (started == "\"started\"") pending.Add(r);
                    else details.AppendLine(r.Name + ": didn't start (" + Short(started) + ")");
                }
                catch (Exception ex) { details.AppendLine(r.Name + ": " + ex.Message); }
            }

            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (pending.Count > 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(150);
                foreach (var r in pending.ToList())
                {
                    try
                    {
                        string raw = await WithTimeout(r.Run(poll), 10);
                        if (string.IsNullOrEmpty(raw) || raw == "null") continue;
                        pending.Remove(r);
                        using var doc = JsonDocument.Parse(JsonSerializer.Deserialize<string>(raw));
                        var root = doc.RootElement;
                        if (root.TryGetProperty("skip", out _)) { details.AppendLine(r.Name + ": not an iCloud page"); continue; }
                        if (root.TryGetProperty("error", out var err)) { details.AppendLine(r.Name + ": error " + Short(err.ToString())); continue; }
                        int count = 0;
                        if (root.TryGetProperty("items", out var items))
                            foreach (var item in items.EnumerateArray()) { pieces.Add(ToPiece(item)); count++; }
                        bool hasSelection = root.TryGetProperty("selection", out var sel) && sel.ValueKind == JsonValueKind.Object;
                        if (hasSelection && selection == null) { selection = ToPiece(sel); selection.FromSelection = true; }
                        details.AppendLine(r.Name + ": " + (root.TryGetProperty("roots", out var rc) ? rc.ToString() : "?") + " roots, " +
                                           (root.TryGetProperty("editable", out var ec) ? ec.ToString() : "?") + " editable, " + count + " visible" + (hasSelection ? ", has a selection" : ""));
                    }
                    catch (Exception ex)
                    {
                        pending.Remove(r);
                        details.AppendLine(r.Name + ": " + ex.Message);
                    }
                }
            }
            foreach (var r in pending) details.AppendLine(r.Name + ": timed out");
            return (pieces, selection);
        }

        // ------------------------------------------------------------ helpers

        private static Piece ToPiece(JsonElement v)
        {
            var p = new Piece
            {
                Visible = v.TryGetProperty("visible", out var vis) && vis.ValueKind == JsonValueKind.True,
                W = v.TryGetProperty("w", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : 0,
                H = v.TryGetProperty("h", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt32() : 0,
                Title = v.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.True,
                Html = v.TryGetProperty("html", out var html) && html.ValueKind == JsonValueKind.String ? html.GetString() : "",
                Text = v.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : "",
                FromSelection = v.TryGetProperty("fromSelection", out var fs) && fs.ValueKind == JsonValueKind.True
            };
            if (v.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array)
                foreach (var x in warnings.EnumerateArray()) if (x.ValueKind == JsonValueKind.String) p.Warnings.Add(x.GetString());
            return p;
        }

        private static Task<string> Cdp(CoreWebView2 core, string method, object parameters) =>
            WithTimeout(core.CallDevToolsProtocolMethodAsync(method, JsonSerializer.Serialize(parameters)), 40);

        private static async Task<string> WithTimeout(Task<string> task, int seconds)
        {
            if (await Task.WhenAny(task, Task.Delay(seconds * 1000)) != task) throw new TimeoutException("timed out");
            return await task;
        }

        private static string Short(string s)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
            return s.Length > 160 ? s.Substring(0, 160) + "…" : s;
        }
    }
}
