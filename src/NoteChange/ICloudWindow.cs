using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NoteChange.Core;

namespace NoteChange
{
    /// <summary>iCloud Notes in its own window, with an "Add this note" bar on top.</summary>
    public sealed class ICloudWindow : Window
    {
        private const string Start = "https://www.icloud.com/notes";
        private readonly WebView2 _web = new WebView2();
        private readonly TextBlock _status;
        private readonly Button _add;
        private readonly Action<NoteSource> _onNote;
        private readonly List<CoreWebView2Frame> _frames = new List<CoreWebView2Frame>();
        private bool _busy;

        public ICloudWindow(Window owner, Action<NoteSource> onNote)
        {
            _onNote = onNote;
            Title = "iCloud Notes · NoteChange";
            Width = 1180;
            Height = 860;
            MinWidth = 820;
            MinHeight = 560;
            Background = Theme.Bg;
            Foreground = Theme.Text;
            FontFamily = Theme.Font;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = null;
            try { Icon = App.IconImage; } catch { }
            if (owner != null && owner.IsVisible)
            {
                Left = owner.Left + 40;
                Top = owner.Top + 30;
                WindowStartupLocation = WindowStartupLocation.Manual;
            }

            _add = new Button { Content = "Add this note", Style = (Style)Application.Current.Resources["Primary"], Padding = new Thickness(18, 9, 18, 9), IsEnabled = false };
            _add.ToolTip = "Copies the note that's open below into your NoteChange collection (Ctrl+Enter)";
            _add.Click += async (s, e) => await AddNote();
            var reload = new Button { Content = "Reload", Style = (Style)Application.Current.Resources["Secondary"], Margin = new Thickness(8, 0, 0, 0) };
            reload.Click += (s, e) => { try { _web.CoreWebView2?.Reload(); } catch { } };
            var back = new Button { Content = "Back to NoteChange", Style = (Style)Application.Current.Resources["Quiet"] };
            back.Click += (s, e) => { Application.Current.MainWindow?.Activate(); };

            _status = new TextBlock
            {
                Text = "Sign in with your Apple Account, open a note, then press Add this note.",
                Foreground = Theme.Muted,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(16, 0, 12, 0)
            };

            var bar = new DockPanel { LastChildFill = true };
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(_add);
            left.Children.Add(reload);
            DockPanel.SetDock(left, Dock.Left);
            DockPanel.SetDock(back, Dock.Right);
            bar.Children.Add(left);
            bar.Children.Add(back);
            bar.Children.Add(_status);

            var top = new Border
            {
                Background = Theme.Side,
                BorderBrush = Theme.Line,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 10, 14, 10),
                Child = bar
            };
            var root = new DockPanel();
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
            root.Children.Add(new Border { Background = Brushes.White, Child = _web });
            Content = root;

            SourceInitialized += (s, e) => Native.StyleTitleBar(this);
            Loaded += async (s, e) => await Init();
            PreviewKeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control && _add.IsEnabled) { e.Handled = true; await AddNote(); }
            };
        }

        private async Task Init()
        {
            try
            {
                var env = await Browser.ICloudEnvironment();
                await _web.EnsureCoreWebView2Async(env);
                var core = _web.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.Settings.IsPasswordAutosaveEnabled = false;

                core.NavigationStarting += (s, e) =>
                {
                    if (!AppleUrl(e.Uri) && e.Uri != "about:blank")
                    {
                        e.Cancel = true;
                        if (e.IsUserInitiated && IsWebLink(e.Uri)) Native.OpenUrl(e.Uri);
                    }
                };
                core.NavigationCompleted += (s, e) =>
                {
                    _add.IsEnabled = true;
                    if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                        Status("iCloud didn't load. Check your internet connection and press Reload.");
                };
                core.NewWindowRequested += (s, e) =>
                {
                    // Apple's sign-in pop-ups open as browser windows so they keep their link to this page.
                    if (AppleUrl(e.Uri) || e.Uri == "about:blank") return;
                    e.Handled = true;
                    if (IsWebLink(e.Uri)) Native.OpenUrl(e.Uri);
                };
                core.PermissionRequested += (s, e) => e.State = CoreWebView2PermissionState.Deny;
                core.FrameCreated += (s, e) => Track(e.Frame);
                core.Navigate(Start);
            }
            catch (WebView2RuntimeNotFoundException)
            {
                MessageBox.Show(this, Browser.MissingRuntime, "NoteChange", MessageBoxButton.OK, MessageBoxImage.Warning);
                Native.OpenUrl(Browser.RuntimeUrl);
                Close();
            }
            catch (Exception ex)
            {
                Log.Write("iCloud window failed: " + ex);
                Status("iCloud couldn't be opened here. Close this window and try again.");
            }
        }

        private void Track(CoreWebView2Frame frame)
        {
            _frames.Add(frame);
            frame.Destroyed += (s, e) => _frames.Remove(frame);
            try { frame.FrameCreated += (s, e) => Track(e.Frame); }
            catch { /* Older WebView2 runtimes don't report frames inside frames. */ }
        }

        public static bool AppleUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https") return false;
            string h = u.Host.ToLowerInvariant();
            foreach (var d in new[] { "icloud.com", "apple.com", "icloud-content.com", "cdn-apple.com", "apple-cloudkit.com", "mzstatic.com" })
                if (h == d || h.EndsWith("." + d)) return true;
            return false;
        }

        private static bool IsWebLink(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http" || u.Scheme == "mailto");

        private void Status(string text) => _status.Text = text;

        /// <summary>Reads the open note and adds it to the collection.</summary>
        public async Task AddNote()
        {
            if (_busy || _web.CoreWebView2 == null) return;
            _busy = true;
            _add.IsEnabled = false;
            Status("Reading the note…");
            try
            {
                var (source, error) = await CaptureRunner.Run(_web.CoreWebView2, _frames.ToList());
                if (source == null) { Status(error); return; }
                var imported = Importer.Import(source);
                _onNote(source);
                Status("Added \u201C" + (imported.Title.Length > 0 ? imported.Title : "Untitled note") + "\u201D. Open another note to add more.");
            }
            catch (NoteException ex) { Status(ex.Message); }
            catch (Exception ex)
            {
                Log.Write("Capture failed: " + ex);
                Status("NoteChange couldn't read that note. Copy it and use Paste note instead.");
            }
            finally
            {
                _busy = false;
                _add.IsEnabled = true;
            }
        }
    }

    /// <summary>Runs Capture.js in the page and its frames and keeps the one clear result.</summary>
    public static class CaptureRunner
    {
        private static string _script;

        private static string Script
        {
            get
            {
                if (_script != null) return _script;
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NoteChange.Capture.js");
                using var reader = new StreamReader(stream);
                return _script = reader.ReadToEnd();
            }
        }

        private sealed class CaptureResult
        {
            public string html { get; set; }
            public string text { get; set; }
            public List<string> warnings { get; set; }
            public string error { get; set; }
        }

        public static async Task<(NoteSource Source, string Error)> Run(CoreWebView2 core, IList<CoreWebView2Frame> frames)
        {
            string token = Guid.NewGuid().ToString("N");
            string script = Script.Replace("__TOKEN__", JsonSerializer.Serialize(token));
            string poll = "(function(){var n=window.__notechange;return (n&&n.token===" + JsonSerializer.Serialize(token) +
                          "&&n.done)?JSON.stringify(n.result):null;})()";

            var runners = new List<Func<string, Task<string>>> { s => core.ExecuteScriptAsync(s) };
            foreach (var f in frames) runners.Add(s => f.ExecuteScriptAsync(s));

            var started = new List<Func<string, Task<string>>>();
            foreach (var run in runners)
            {
                try
                {
                    string r = await run(script);
                    if (r == "\"started\"") started.Add(run);
                }
                catch (Exception ex) { Log.Write("Frame skipped: " + ex.Message); }
            }
            if (started.Count == 0) return (null, "Open iCloud Notes and choose a note first.");

            var results = new List<CaptureResult>();
            var deadline = DateTime.UtcNow.AddSeconds(25);
            var pending = new List<Func<string, Task<string>>>(started);
            while (pending.Count > 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(150);
                foreach (var run in pending.ToList())
                {
                    try
                    {
                        string raw = await run(poll);
                        if (raw == "null" || string.IsNullOrEmpty(raw)) continue;
                        string json = JsonSerializer.Deserialize<string>(raw);
                        results.Add(JsonSerializer.Deserialize<CaptureResult>(json));
                        pending.Remove(run);
                    }
                    catch (Exception ex)
                    {
                        Log.Write("Poll failed: " + ex.Message);
                        pending.Remove(run);
                    }
                }
            }

            var good = results.Where(r => r != null && r.error == null && (!string.IsNullOrWhiteSpace(r.text) || !string.IsNullOrWhiteSpace(r.html))).ToList();
            if (good.Count != 1)
            {
                string why = good.Count > 1
                    ? "More than one note is open. Close the others and try again."
                    : results.Select(r => r?.error).FirstOrDefault(e => e != null)
                      ?? (pending.Count > 0 ? "Reading the note took too long. Try again." : "NoteChange couldn't read that note. Copy it and use Paste note instead.");
                return (null, why);
            }

            var source = NoteSource.FromHtml(good[0].html ?? "", good[0].text ?? "", "icloud");
            if (good[0].warnings != null) source.Warnings.AddRange(good[0].warnings.Distinct());
            return (source, null);
        }
    }
}
