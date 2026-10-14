using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.Web.WebView2.Core;
using NoteChange.Core;

namespace NoteChange
{
    /// <summary>
    /// Checks the real, packaged app end to end: the window, the preview, every export format and the
    /// iCloud note reader against a stand-in page. Run with --self-test [folder]; --screenshots also
    /// saves pictures of the window for the website.
    /// </summary>
    public static class SelfTest
    {
        public static bool Running { get; private set; }
        private static readonly List<string> Failures = new List<string>();
        private static readonly StringBuilder Report = new StringBuilder();

        public static void Fail(string message)
        {
            Failures.Add(message);
            Report.AppendLine("FAIL  " + message);
        }

        private static void Check(string name, bool ok, string detail = "")
        {
            if (ok) Report.AppendLine("ok    " + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
            else Fail(name + (detail.Length > 0 ? ": " + detail : ""));
        }

        public static async Task<int> Run(string output, bool screenshots)
        {
            Running = true;
            Directory.CreateDirectory(output);
            Report.AppendLine("NoteChange " + Updates.CurrentText + " self-test, " + DateTime.Now.ToString("u"));
            MainWindow window = null;
            try
            {
                string runtime = Browser.RuntimeVersion();
                Check("WebView2 runtime", runtime != null, runtime ?? "missing");

                window = new MainWindow(new Settings { CheckForUpdates = false })
                {
                    ScreenshotMode = true,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = 0,
                    Top = 0,
                    Width = 1440,
                    Height = 900
                };
                Application.Current.MainWindow = window;
                window.Show();
                Check("Welcome page renders", await Wait(window.Rendered));
                if (screenshots) await Shot(window, Path.Combine(output, "welcome.png"));

                Check("Recipe added", window.AddNote(Samples.Recipe(), false));
                Check("Recipe preview renders", await Wait(window.Rendered));
                Check("Chapter added", window.AddNote(Samples.Chapter(), false));
                Check("Chapter preview renders", await Wait(window.Rendered));
                if (screenshots) await Shot(window, Path.Combine(output, "chapter.png"));
                Check("Checklist added", window.AddNote(Samples.Checklist(), false));
                Check("Checklist preview renders", await Wait(window.Rendered));
                if (screenshots) await Shot(window, Path.Combine(output, "checklist.png"));
                window.SelectNote(0);
                Check("Recipe preview renders again", await Wait(window.Rendered));
                if (screenshots) await Shot(window, Path.Combine(output, "recipe.png"));

                string html = await window.PreviewCore.ExecuteScriptAsync("document.body.innerText");
                Check("Preview shows the recipe", html.Contains("Lemon drizzle cake") && html.Contains("INGREDIENTS", StringComparison.OrdinalIgnoreCase));

                var notes = Formatter.FormatAll(new[] { Samples.Recipe(), Samples.Chapter(), Samples.Checklist() }, new FormatOptions());

                string docx = Path.Combine(output, "collection.docx");
                await window.ExportTo(ExportKind.Word, notes, docx);
                using (var doc = WordprocessingDocument.Open(docx, false))
                {
                    var errors = new OpenXmlValidator().Validate(doc).ToList();
                    Check("Word file is valid", errors.Count == 0, errors.Count == 0 ? new FileInfo(docx).Length + " bytes" : string.Join("; ", errors.Take(5).Select(e => e.Description)));
                    string text = doc.MainDocumentPart.Document.Body.InnerText;
                    Check("Word file has every note", text.Contains("Lemon drizzle cake") && text.Contains("Gull Point") && text.Contains("Catbells"));
                }

                string pdf = Path.Combine(output, "collection.pdf");
                await window.ExportTo(ExportKind.Pdf, notes, pdf);
                byte[] pdfBytes = File.ReadAllBytes(pdf);
                int pages = Regex.Matches(Encoding.ASCII.GetString(pdfBytes), @"/Type\s*/Page[^s]").Count;
                Check("PDF is written", pdfBytes.Length > 4000 && Encoding.ASCII.GetString(pdfBytes, 0, 5) == "%PDF-", pdfBytes.Length + " bytes");
                Check("PDF starts each note on a new page", pages >= 3, pages + " pages");

                string eml = Path.Combine(output, "note.eml");
                await window.ExportTo(ExportKind.Email, notes.Take(1).ToList(), eml);
                Check("Email draft is written", File.ReadAllText(eml).StartsWith("X-Unsent: 1"));

                string md = Path.Combine(output, "note.md");
                await window.ExportTo(ExportKind.Markdown, notes.Skip(2).ToList(), md);
                Check("Markdown is written", File.ReadAllText(md).Contains("- [x] Walking boots"));

                string txt = Path.Combine(output, "note.txt");
                await window.ExportTo(ExportKind.Text, notes.Skip(1).Take(1).ToList(), txt);
                Check("Text is written", File.ReadAllText(txt).Contains("The keeper of Gull Point"));

                await CaptureCheck(window);

                Check("Clipboard HTML format", ClipboardHtml.Build("<b>é</b>").Contains("StartFragment:0000000"));
            }
            catch (Exception ex)
            {
                Fail("Crashed: " + ex);
            }
            finally
            {
                Report.AppendLine(Failures.Count == 0 ? "PASSED" : "FAILED (" + Failures.Count + ")");
                File.WriteAllText(Path.Combine(output, "self-test.txt"), Report.ToString());
                try { window?.Close(); } catch { }
            }
            return Failures.Count == 0 ? 0 : 1;
        }

        private static async Task<bool> Wait(Task<bool> task, int seconds = 30)
        {
            var done = await Task.WhenAny(task, Task.Delay(seconds * 1000));
            if (done != task) return false;
            await Task.Delay(500);
            return task.Result;
        }

        private const string Gif1x1 = "R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==";
        private const string Png1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        /// <summary>Runs the note reader against a stand-in iCloud page with the editor inside a frame.</summary>
        private static async Task CaptureCheck(Window owner)
        {
            var env = await Browser.PreviewEnvironment();
            var controller = await env.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(owner).EnsureHandle());
            try
            {
                controller.Bounds = new System.Drawing.Rectangle(0, 0, 900, 700);
                controller.IsVisible = true;
                var core = controller.CoreWebView2;
                var frames = new List<CoreWebView2Frame>();
                core.FrameCreated += (s, e) => frames.Add(e.Frame);

                string top = "<!doctype html><html><body style='margin:0'><iframe src='https://www.icloud.com/applications/notes3/index.html' style='width:860px;height:640px;border:0'></iframe></body></html>";
                string editor = "<!doctype html><html><body style='margin:0;font-family:sans-serif'>" +
                    "<div contenteditable='true' style='width:800px;min-height:400px'>" +
                    "<div><b>Garden jobs</b></div>" +
                    "<div>Prune the <span style='font-weight:700'>roses</span> and <span style='font-style:italic'>water</span> the pots.</div>" +
                    "<ul><li><span role='checkbox' aria-checked='true'></span>Mow the lawn</li><li><span role='checkbox' aria-checked='false'></span>Weed the beds</li></ul>" +
                    "<div><img src='data:image/png;base64," + Png1x1 + "' alt='dot' style='width:40px;height:40px'></div>" +
                    "<div><img src='data:image/gif;base64," + Gif1x1 + "' alt='gif' style='width:40px;height:40px'></div>" +
                    "</div></body></html>";

                var env2 = env;
                core.AddWebResourceRequestedFilter("https://www.icloud.com/*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += (s, e) =>
                {
                    string page = e.Request.Uri.Contains("/applications/") ? editor : top;
                    e.Response = env2.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(page)), 200, "OK", "Content-Type: text/html; charset=utf-8");
                };
                var loaded = new TaskCompletionSource<bool>();
                core.NavigationCompleted += (s, e) => loaded.TrySetResult(e.IsSuccess);
                core.Navigate("https://www.icloud.com/notes/");
                await Wait(loaded.Task);
                await Task.Delay(1500);

                var (source, error) = await CaptureRunner.Run(core, frames);
                Check("Reads a note inside an iCloud frame", source != null, error ?? frames.Count + " frame(s)");
                if (source == null) return;
                var doc = Formatter.Format(Importer.Import(source), new FormatOptions());
                Check("Note title", doc.Title == "Garden jobs", doc.Title);
                var runs = doc.Blocks.SelectMany(b => b.Runs).ToList();
                Check("Bold and italic from styles", runs.Any(r => r.Text.Contains("roses") && r.Bold) && runs.Any(r => r.Text.Contains("water") && r.Italic));
                var items = doc.Blocks.Where(b => b.Kind == BlockKind.ListItem).ToList();
                Check("Checklist ticks", items.Count == 2 && items[0].PlainText.StartsWith("☑") && items[1].PlainText.StartsWith("☐"),
                    string.Join(" / ", items.Select(i => i.PlainText)));
                var images = runs.Where(r => r.IsImage).ToList();
                Check("Pictures come across (PNG kept, GIF converted)", images.Count == 2 && images.All(i => i.Image.Format == "png"), images.Count + " picture(s)");
            }
            finally
            {
                controller.Close();
            }
        }

        /// <summary>Saves the window as a PNG: the WPF parts plus a capture of the preview.</summary>
        private static async Task Shot(MainWindow window, string path)
        {
            window.UpdateLayout();
            await Task.Delay(900);
            var content = (FrameworkElement)window.Content;
            var dpi = VisualTreeHelper.GetDpi(window);
            int width = (int)Math.Round(content.ActualWidth * dpi.DpiScaleX);
            int height = (int)Math.Round(content.ActualHeight * dpi.DpiScaleY);

            var chrome = new RenderTargetBitmap(width, height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            chrome.Render(content);

            BitmapSource web = null;
            try
            {
                using var ms = new MemoryStream();
                await window.PreviewCore.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
                ms.Position = 0;
                web = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
            catch (Exception ex) { Report.AppendLine("note  preview capture failed: " + ex.Message); }

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(chrome, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
                if (web != null)
                {
                    var p = window.PreviewElement.TranslatePoint(new Point(0, 0), content);
                    dc.DrawImage(web, new Rect(p.X, p.Y, window.PreviewElement.ActualWidth, window.PreviewElement.ActualHeight));
                }
            }
            var final = new RenderTargetBitmap(width, height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            final.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(final));
            using (var f = File.Create(path)) encoder.Save(f);
            Report.AppendLine("shot  " + Path.GetFileName(path));
        }
    }
}
