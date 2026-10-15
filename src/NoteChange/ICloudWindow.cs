using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Button _copyDetails;
        private string _details;
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
            _copyDetails = new Button { Content = "Copy details", Style = (Style)Application.Current.Resources["Quiet"], Visibility = Visibility.Collapsed, Foreground = Theme.Amber };
            _copyDetails.ToolTip = "Copies a description of how this page is built (never your note's text), to help fix the problem";
            _copyDetails.Click += (s, e) =>
            {
                try { Clipboard.SetText(_details ?? ""); Status("Details copied. Paste them into an issue on GitHub, or send them to whoever helps you with NoteChange."); }
                catch { Status("Couldn't copy the details. Try again."); }
            };
            back.Click += (s, e) => { Application.Current.MainWindow?.Activate(); };

            _status = new TextBlock
            {
                Text = "Sign in with your Apple Account, open a note, then press Add this note.",
                Foreground = Theme.Muted,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16, 0, 12, 0)
            };

            var bar = new DockPanel { LastChildFill = true };
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(_add);
            left.Children.Add(reload);
            DockPanel.SetDock(left, Dock.Left);
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            right.Children.Add(_copyDetails);
            right.Children.Add(back);
            DockPanel.SetDock(right, Dock.Right);
            bar.Children.Add(left);
            bar.Children.Add(right);
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
            _copyDetails.Visibility = Visibility.Collapsed;
            Status("Reading the note…");
            try
            {
                var result = await CaptureRunner.Run(_web.CoreWebView2, _frames.ToList());
                _details = result.Details;
                Log.Write(result.Details);
                if (result.Source == null)
                {
                    Status(result.Error);
                    _copyDetails.Visibility = Visibility.Visible;
                    return;
                }
                var imported = Importer.Import(result.Source);
                _onNote(result.Source);
                Status("Added \u201C" + (imported.Title.Length > 0 ? imported.Title : "Untitled note") + "\u201D. Open another note to add more.");
            }
            catch (NoteException ex) { Status(ex.Message); }
            catch (Exception ex)
            {
                Log.Write("Capture failed: " + ex);
                _details = "Capture failed: " + ex.GetType().Name + ": " + ex.Message;
                _copyDetails.Visibility = Visibility.Visible;
                Status(CaptureRunner.NotFound);
            }
            finally
            {
                _busy = false;
                _add.IsEnabled = true;
            }
        }
    }
}
