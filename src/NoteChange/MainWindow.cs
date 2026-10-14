using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using NoteChange.Core;

namespace NoteChange
{
    /// <summary>One note in the collection list.</summary>
    public sealed class NoteItem : INotifyPropertyChanged
    {
        private string _detail;
        public NoteItem(NoteSource source, string title) { Source = source; Title = string.IsNullOrWhiteSpace(title) ? "Untitled note" : title; }
        public NoteSource Source { get; }
        public string Title { get; }
        public string Detail { get => _detail; set { _detail = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail))); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public enum ExportKind { Word, Pdf, Text, Markdown, Email }

    /// <summary>The NoteChange window: your notes on the left, the preview in the middle, style and export on the right.</summary>
    public sealed class MainWindow : Window
    {
        private readonly Settings _settings;
        private readonly ObservableCollection<NoteItem> _notes = new ObservableCollection<NoteItem>();
        private readonly ICollectionView _view;

        private readonly ListBox _list;
        private readonly TextBox _search;
        private readonly TextBlock _emptyList;
        private readonly TextBlock _count;
        private readonly Button _up, _down, _remove, _saveProject, _clear;

        private readonly Dictionary<Layout, RadioButton> _layouts = new Dictionary<Layout, RadioButton>();
        private readonly Dictionary<TitleMode, RadioButton> _titles = new Dictionary<TitleMode, RadioButton>();
        private readonly TextBox _customTitle;
        private readonly CheckBox _largeText, _combine;
        private readonly List<Button> _exports = new List<Button>();
        private readonly Border _warningBox;
        private readonly TextBlock _warningText;
        private readonly TextBlock _layoutLabel;

        private readonly WebView2 _preview = new WebView2();
        private PageHost _page;
        private string _shownKey;
        private double _scroll;
        private TaskCompletionSource<bool> _rendered = new TaskCompletionSource<bool>();

        private readonly TextBlock _status;
        private readonly Button _showFile, _update;
        private string _lastFile;

        private ICloudWindow _icloud;
        private bool _dirty;
        private bool _ready;
        private bool _closingConfirmed;

        public bool ScreenshotMode { get; set; }
        public CoreWebView2 PreviewCore => _preview.CoreWebView2;
        public FrameworkElement PreviewElement => _preview;
        public void SelectNote(int index) { if (index >= 0 && index < _notes.Count) _list.SelectedItem = _notes[index]; }

        public MainWindow(Settings settings)
        {
            _settings = settings;
            Title = "NoteChange";
            Width = settings.Width >= 960 ? settings.Width : 1360;
            Height = settings.Height >= 640 ? settings.Height : 880;
            MinWidth = 1000;
            MinHeight = 640;
            Background = Theme.Bg;
            Foreground = Theme.Text;
            FontFamily = Theme.Font;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (settings.Maximized) WindowState = WindowState.Maximized;
            try { Icon = App.IconImage; } catch { }
            AllowDrop = true;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            // ------------------------------------------------ left: your notes
            var openICloud = Btn("Open iCloud Notes", "Primary", (s, e) => OpenICloud());
            openICloud.Padding = new Thickness(14, 11, 14, 11);
            openICloud.ToolTip = "Sign in to iCloud, open a note and press Add this note";
            var paste = Btn("Paste note", "Secondary", (s, e) => PasteNote());
            paste.ToolTip = "Add whatever you've copied (Ctrl+V)";
            var openFile = Btn("Open file", "Secondary", async (s, e) => await OpenFiles());
            openFile.ToolTip = "Open a saved collection, or a text, Markdown or web page file (Ctrl+O)";

            _count = new TextBlock { Foreground = Theme.Faint, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var listHeader = new Grid { Margin = new Thickness(0, 26, 0, 10) };
            listHeader.Children.Add(Label("YOUR COLLECTION"));
            listHeader.Children.Add(_count);

            _search = new TextBox { ToolTip = "Find a note by its title" };
            _search.TextChanged += (s, e) => { _view.Refresh(); };
            var searchBox = WithPlaceholder(_search, "Search your notes");

            _list = new ListBox { Margin = new Thickness(-4, 10, -4, 0) };
            _list.ItemTemplate = (DataTemplate)XamlReader.Parse(
                "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
                "<StackPanel><TextBlock Text='{Binding Title}' FontWeight='SemiBold' FontSize='13.5' TextTrimming='CharacterEllipsis'/>" +
                "<TextBlock Text='{Binding Detail}' FontSize='12' Foreground='#A7ABB5' Margin='0,2,0,0'/></StackPanel></DataTemplate>");
            _view = CollectionViewSource.GetDefaultView(_notes);
            _view.Filter = o => _search.Text.Length == 0 || ((NoteItem)o).Title.IndexOf(_search.Text, StringComparison.CurrentCultureIgnoreCase) >= 0;
            _list.ItemsSource = _view;
            _list.SelectionChanged += (s, e) => { UpdateButtons(); Refresh(); };
            _list.KeyDown += (s, e) => { if (e.Key == Key.Delete) { RemoveSelected(); e.Handled = true; } };
            ScrollViewer.SetCanContentScroll(_list, false);

            _emptyList = new TextBlock
            {
                Text = "Nothing here yet.\n\nOpen iCloud Notes, choose a note and press Add this note. You can also paste a note, or try an example in the middle.",
                Foreground = Theme.Muted,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                Margin = new Thickness(2, 14, 8, 0)
            };
            var listArea = new Grid();
            listArea.Children.Add(_list);
            listArea.Children.Add(_emptyList);

            _up = Btn("↑", "Secondary", (s, e) => Move(-1));
            _up.ToolTip = "Move up (Alt+Up)";
            _down = Btn("↓", "Secondary", (s, e) => Move(1));
            _down.ToolTip = "Move down (Alt+Down)";
            _remove = Btn("Remove", "Secondary", (s, e) => RemoveSelected());
            _remove.ToolTip = "Take this note out of the collection. Your note in iCloud isn't changed.";
            var order = Row(6, _up, _down, _remove);
            order.Margin = new Thickness(0, 10, 0, 0);

            _saveProject = Btn("Save collection", "Secondary", async (s, e) => await SaveProject());
            _saveProject.ToolTip = "Save these notes in a file you can open again later";
            _clear = Btn("Clear", "Quiet", (s, e) => ClearAll());
            var bottom = Row(6, _saveProject, _clear);
            bottom.Margin = new Thickness(0, 12, 0, 0);

            var leftTop = new StackPanel();
            leftTop.Children.Add(Logo());
            leftTop.Children.Add(openICloud);
            var addRow = Row(8, paste, openFile);
            addRow.Margin = new Thickness(0, 8, 0, 0);
            leftTop.Children.Add(addRow);
            leftTop.Children.Add(listHeader);
            leftTop.Children.Add(searchBox);

            var leftBottom = new StackPanel();
            leftBottom.Children.Add(order);
            leftBottom.Children.Add(bottom);

            var left = new DockPanel { Margin = new Thickness(20, 20, 20, 18) };
            DockPanel.SetDock(leftTop, Dock.Top);
            DockPanel.SetDock(leftBottom, Dock.Bottom);
            left.Children.Add(leftTop);
            left.Children.Add(leftBottom);
            left.Children.Add(listArea);
            var leftPanel = new Border { Width = 296, Background = Theme.Side, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 1, 0), Child = left };

            // ------------------------------------------------ right: style and export
            var right = new StackPanel { Margin = new Thickness(22, 22, 22, 22) };
            right.Children.Add(Label("STYLE"));
            var layoutGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 10, -6, 0) };
            foreach (var (layout, text, tip) in new[]
            {
                (Layout.Auto, "Automatic", "NoteChange picks a layout from what's in the note"),
                (Layout.Document, "Document", "A clean, simple document"),
                (Layout.Recipe, "Recipe", "Ingredients, Method and Notes become headings"),
                (Layout.Chapter, "Chapter", "Book typography: serif text, indented paragraphs")
            })
            {
                var rb = Segment(text, "layout", tip);
                rb.Checked += (s, e) => { if (_ready) { SaveChoices(); Refresh(); } };
                _layouts[layout] = rb;
                layoutGrid.Children.Add(rb);
            }
            right.Children.Add(layoutGrid);
            _layoutLabel = new TextBlock { Foreground = Theme.Muted, FontSize = 12, Margin = new Thickness(2, 2, 0, 0) };
            right.Children.Add(_layoutLabel);

            var titleLabel = Label("TITLE");
            titleLabel.Margin = new Thickness(0, 22, 0, 0);
            right.Children.Add(titleLabel);
            var titleGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 10, -6, 0) };
            foreach (var (mode, text, tip) in new[]
            {
                (TitleMode.FirstLine, "First line", "Use the note's first line as the title"),
                (TitleMode.Custom, "My own", "Type a title of your own"),
                (TitleMode.None, "None", "No title: keep every line in the body")
            })
            {
                var rb = Segment(text, "title", tip);
                rb.Checked += (s, e) => { if (_ready) { _customTitle.Visibility = _titles[TitleMode.Custom].IsChecked == true ? Visibility.Visible : Visibility.Collapsed; SaveChoices(); Refresh(); } };
                _titles[mode] = rb;
                titleGrid.Children.Add(rb);
            }
            right.Children.Add(titleGrid);
            _customTitle = new TextBox { Margin = new Thickness(0, 2, 0, 0), Visibility = Visibility.Collapsed, ToolTip = "Your title" };
            _customTitle.TextChanged += (s, e) => Refresh();
            right.Children.Add(WithPlaceholder(_customTitle, "Type your title"));

            _largeText = new CheckBox { Content = "Larger text", Margin = new Thickness(0, 20, 0, 0), ToolTip = "14 point instead of 12 point" };
            _largeText.Checked += (s, e) => { if (_ready) { SaveChoices(); Refresh(); } };
            _largeText.Unchecked += (s, e) => { if (_ready) { SaveChoices(); Refresh(); } };
            right.Children.Add(_largeText);
            _combine = new CheckBox
            {
                Content = new TextBlock { Text = "Whole collection as one document", TextWrapping = TextWrapping.Wrap },
                Margin = new Thickness(0, 12, 0, 0),
                ToolTip = "Every note in order, each starting on a new page"
            };
            _combine.Checked += (s, e) => { if (_ready) { SaveChoices(); Refresh(); } };
            _combine.Unchecked += (s, e) => { if (_ready) { SaveChoices(); Refresh(); } };
            right.Children.Add(_combine);

            right.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(0, 22, 0, 22) });
            right.Children.Add(Label("SAVE OR SHARE"));
            var word = Export(Btn("Save as Word", "Primary", async (s, e) => await ExportWithDialog(ExportKind.Word)));
            word.Margin = new Thickness(0, 10, 0, 0);
            word.Padding = new Thickness(14, 11, 14, 11);
            word.ToolTip = "A .docx file for Word, Pages or Google Docs (Ctrl+S)";
            right.Children.Add(word);
            var pdf = Export(Btn("Save as PDF", "Secondary", async (s, e) => await ExportWithDialog(ExportKind.Pdf)));
            pdf.Margin = new Thickness(0, 8, 0, 0);
            pdf.ToolTip = "An A4 PDF, ready to print or send (Ctrl+P)";
            right.Children.Add(pdf);
            var copy = Export(Btn("Copy for email", "Secondary", (s, e) => CopyForEmail()));
            copy.Margin = new Thickness(0, 8, 0, 0);
            copy.ToolTip = "Copies the note with its formatting. Paste it into a new email.";
            right.Children.Add(copy);

            var more = Label("MORE FORMATS");
            more.Margin = new Thickness(0, 20, 0, 8);
            right.Children.Add(more);
            var email = Export(Btn("New email", "Secondary", (s, e) => OpenEmail()));
            email.ToolTip = "Opens your email app with the note as the message";
            var eml = Export(Btn("Email draft", "Secondary", async (s, e) => await ExportWithDialog(ExportKind.Email)));
            eml.ToolTip = "An .eml draft with pictures that opens in Outlook or Mail";
            var txt = Export(Btn("Text", "Secondary", async (s, e) => await ExportWithDialog(ExportKind.Text)));
            txt.ToolTip = "A plain .txt file";
            var md = Export(Btn("Markdown", "Secondary", async (s, e) => await ExportWithDialog(ExportKind.Markdown)));
            md.ToolTip = "A .md file for notes apps and websites";
            var formats = new UniformGrid { Columns = 2 };
            foreach (var b in new[] { email, eml, txt, md }) { b.Margin = new Thickness(0, 0, 6, 6); b.Padding = new Thickness(8, 8, 8, 8); formats.Children.Add(b); }
            formats.Margin = new Thickness(0, 0, -6, 0);
            right.Children.Add(formats);

            _warningText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Theme.Text, FontSize = 12.5, LineHeight = 18 };
            _warningBox = new Border
            {
                Background = Theme.AmberTint,
                BorderBrush = Theme.Amber,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 16, 0, 0),
                Visibility = Visibility.Collapsed,
                Child = _warningText
            };
            AutomationProperties.SetLiveSetting(_warningText, AutomationLiveSetting.Polite);
            right.Children.Add(_warningBox);

            var rightScroll = new ScrollViewer { Content = right, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var rightPanel = new Border { Width = 312, Background = Theme.Side, BorderBrush = Theme.Line, BorderThickness = new Thickness(1, 0, 0, 0), Child = rightScroll };

            // ------------------------------------------------ status bar
            _status = new TextBlock { Foreground = Theme.Muted, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
            _showFile = Btn("Show file", "Quiet", (s, e) => ShowLastFile());
            _showFile.Visibility = Visibility.Collapsed;
            _showFile.Foreground = Theme.Amber;
            _update = Btn("Update", "Quiet", async (s, e) => await InstallUpdate());
            _update.Visibility = Visibility.Collapsed;
            _update.Foreground = Theme.Amber;
            var version = new TextBlock { Text = "NoteChange " + Updates.CurrentText, Foreground = Theme.Faint, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0) };
            var checkUpdates = Btn("Check for updates", "Quiet", async (s, e) => await CheckUpdates(true));
            var signOut = Btn("Sign out of iCloud", "Quiet", async (s, e) => await SignOut());
            var help = Btn("Help", "Quiet", (s, e) => new HelpWindow(this).ShowDialog());
            var statusRight = Row(0, _update, version, checkUpdates, signOut, help);
            var statusLeft = Row(4, _status, _showFile);
            var statusBar = new DockPanel { Margin = new Thickness(18, 0, 10, 0), LastChildFill = true };
            DockPanel.SetDock(statusRight, Dock.Right);
            statusBar.Children.Add(statusRight);
            statusBar.Children.Add(statusLeft);
            var statusBorder = new Border { Height = 40, Background = Theme.Bg, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Child = statusBar };

            // ------------------------------------------------ assemble
            var middle = new Border { Background = Theme.Stage, Child = _preview };
            var main = new DockPanel();
            DockPanel.SetDock(leftPanel, Dock.Left);
            DockPanel.SetDock(rightPanel, Dock.Right);
            main.Children.Add(leftPanel);
            main.Children.Add(rightPanel);
            main.Children.Add(middle);

            var root = new DockPanel();
            DockPanel.SetDock(statusBorder, Dock.Bottom);
            root.Children.Add(statusBorder);
            root.Children.Add(main);
            Content = root;

            // Restore choices.
            Enum.TryParse(settings.Layout, out Layout savedLayout);
            Enum.TryParse(settings.TitleMode, out TitleMode savedTitle);
            if (savedTitle == TitleMode.Custom) savedTitle = TitleMode.FirstLine;
            _layouts[_layouts.ContainsKey(savedLayout) ? savedLayout : Layout.Auto].IsChecked = true;
            _titles[_titles.ContainsKey(savedTitle) ? savedTitle : TitleMode.FirstLine].IsChecked = true;
            _largeText.IsChecked = settings.LargeText;
            _combine.IsChecked = settings.Combine;

            SourceInitialized += (s, e) => Native.StyleTitleBar(this);
            Loaded += async (s, e) => await OnLoaded();
            Closing += OnClosing;
            PreviewKeyDown += OnKey;
            Drop += OnDrop;
            DragOver += (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Html) || e.Data.GetDataPresent(DataFormats.UnicodeText)
                    ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };

            UpdateButtons();
            SetStatus("Ready. Your notes stay on this PC.");
        }

        // ================================================================ start-up

        private async Task OnLoaded()
        {
            try
            {
                var env = await Browser.PreviewEnvironment();
                await _preview.EnsureCoreWebView2Async(env);
                var core = _preview.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = true;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                core.Settings.IsWebMessageEnabled = true;
                _page = new PageHost(core, env);
                core.NavigationStarting += (s, e) =>
                {
                    if (PageHost.IsOwnPage(e.Uri)) return;
                    e.Cancel = true;
                    if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http" || u.Scheme == "mailto")) Native.OpenUrl(e.Uri);
                };
                core.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http" || u.Scheme == "mailto")) Native.OpenUrl(e.Uri);
                };
                core.NavigationCompleted += async (s, e) =>
                {
                    if (_scroll > 0) { try { await core.ExecuteScriptAsync("window.scrollTo(0," + _scroll.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"); } catch { } }
                    _rendered.TrySetResult(true);
                };
                core.WebMessageReceived += (s, e) =>
                {
                    if (!PageHost.IsOwnPage(e.Source)) return;
                    string msg;
                    try { msg = e.TryGetWebMessageAsString(); } catch { return; }
                    if (msg == "example:recipe") AddNote(Samples.Recipe(), markDirty: false);
                    else if (msg == "example:chapter") AddNote(Samples.Chapter(), markDirty: false);
                    else if (msg == "example:checklist") AddNote(Samples.Checklist(), markDirty: false);
                    else if (msg == "icloud") OpenICloud();
                    else if (msg == "paste") PasteNote();
                };
            }
            catch (WebView2RuntimeNotFoundException)
            {
                MessageBox.Show(this, Browser.MissingRuntime, "NoteChange", MessageBoxButton.OK, MessageBoxImage.Warning);
                Native.OpenUrl(Browser.RuntimeUrl);
                Close();
                return;
            }
            catch (Exception ex)
            {
                Log.Write("Preview failed to start: " + ex);
                SetStatus("The preview couldn't start. You can still save documents.");
            }

            _customTitle.Visibility = _titles[TitleMode.Custom].IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            _ready = true;
            Refresh();

            if (_settings.CheckForUpdates && !ScreenshotMode)
            {
                await Task.Delay(4000);
                await CheckUpdates(false);
            }
        }

        // ================================================================ adding notes

        public bool AddNote(NoteSource source, bool markDirty = true)
        {
            try
            {
                if (_notes.Count >= Formatter.MaxNotes)
                {
                    SetStatus("Your collection has " + Formatter.MaxNotes + " notes. Save it, then start a new one.");
                    return false;
                }
                var imported = Importer.Import(source);
                var item = new NoteItem(source, imported.Title);
                _notes.Add(item);
                Renumber();
                _search.Text = "";
                _list.SelectedItem = item;
                _list.ScrollIntoView(item);
                if (markDirty) _dirty = true;
                UpdateButtons();
                SetStatus("Added “" + item.Title + "”." + (imported.Warnings.Count > 0 ? " Check the note on the right before saving." : ""));
                return true;
            }
            catch (NoteException ex)
            {
                SetStatus(ex.Message);
                return false;
            }
        }

        private void OpenICloud()
        {
            if (_icloud == null || !_icloud.IsLoaded)
            {
                _icloud = new ICloudWindow(this, source => Dispatcher.Invoke(() => AddNote(source)));
                _icloud.Closed += (s, e) => _icloud = null;
                _icloud.Show();
            }
            else
            {
                if (_icloud.WindowState == WindowState.Minimized) _icloud.WindowState = WindowState.Normal;
                _icloud.Activate();
            }
        }

        private void PasteNote()
        {
            try
            {
                string html = null, text = null;
                if (Clipboard.ContainsData(DataFormats.Html)) html = ClipboardHtml.Fragment(Clipboard.GetData(DataFormats.Html) as string);
                if (Clipboard.ContainsText()) text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(html) && string.IsNullOrWhiteSpace(text))
                {
                    SetStatus("There's nothing to paste. Copy a note first, then press Paste note.");
                    return;
                }
                AddNote(string.IsNullOrWhiteSpace(html) ? NoteSource.FromText(text) : NoteSource.FromHtml(html, text ?? "", "paste"));
            }
            catch (Exception ex)
            {
                Log.Write("Paste failed: " + ex.Message);
                SetStatus("The clipboard couldn't be read. Copy the note again and retry.");
            }
        }

        private async Task OpenFiles()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open a collection or a note",
                Filter = "Collections and notes|*.json;*.txt;*.md;*.markdown;*.html;*.htm|NoteChange collection|*.json|Text and Markdown|*.txt;*.md;*.markdown|Web page|*.html;*.htm",
                Multiselect = true,
                InitialDirectory = Paths.Documents
            };
            if (dialog.ShowDialog(this) != true) return;
            foreach (var file in dialog.FileNames) await AddFile(file);
        }

        public Task OpenPath(string file) => AddFile(file);

        private async Task AddFile(string file)
        {
            try
            {
                var info = new FileInfo(file);
                if (info.Length > Project.MaxBytes) { SetStatus(info.Name + " is too large to open (32 MB at most)."); return; }
                string content = await File.ReadAllTextAsync(file);
                string ext = info.Extension.ToLowerInvariant();
                if (ext == ".json")
                {
                    var notes = Project.Parse(content);
                    bool wasEmpty = _notes.Count == 0;
                    int added = notes.Count(n => AddNote(n, markDirty: !wasEmpty));
                    SetStatus("Opened " + info.Name + ": " + added + (added == 1 ? " note." : " notes."));
                }
                else if (ext == ".html" || ext == ".htm")
                {
                    AddNote(NoteSource.FromHtml(content, "", "file"));
                }
                else
                {
                    AddNote(NoteSource.FromText(content, "file"));
                }
            }
            catch (NoteException ex) { SetStatus(ex.Message); }
            catch (Exception ex)
            {
                Log.Write("Open failed: " + ex.Message);
                SetStatus("Couldn't open " + Path.GetFileName(file) + ".");
            }
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var f in files) await AddFile(f);
                return;
            }
            string html = e.Data.GetDataPresent(DataFormats.Html) ? ClipboardHtml.Fragment(e.Data.GetData(DataFormats.Html) as string) : null;
            string text = e.Data.GetDataPresent(DataFormats.UnicodeText) ? e.Data.GetData(DataFormats.UnicodeText) as string : null;
            if (!string.IsNullOrWhiteSpace(html)) AddNote(NoteSource.FromHtml(html, text ?? "", "paste"));
            else if (!string.IsNullOrWhiteSpace(text)) AddNote(NoteSource.FromText(text));
        }

        // ================================================================ collection

        private NoteItem Selected => _list.SelectedItem as NoteItem;

        private void Renumber()
        {
            for (int i = 0; i < _notes.Count; i++) _notes[i].Detail = (i + 1) + " · " + _notes[i].Source.OriginLabel;
            _count.Text = _notes.Count == 0 ? "" : _notes.Count + (_notes.Count == 1 ? " note" : " notes");
            _emptyList.Visibility = _notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Move(int delta)
        {
            var item = Selected;
            if (item == null) return;
            int i = _notes.IndexOf(item), j = i + delta;
            if (j < 0 || j >= _notes.Count) return;
            _notes.Move(i, j);
            Renumber();
            _list.SelectedItem = item;
            _dirty = true;
            UpdateButtons();
            Refresh();
        }

        private void RemoveSelected()
        {
            var item = Selected;
            if (item == null) return;
            int i = _notes.IndexOf(item);
            _notes.Remove(item);
            Renumber();
            if (_notes.Count > 0) _list.SelectedItem = _notes[Math.Min(i, _notes.Count - 1)];
            _dirty = _notes.Count > 0;
            UpdateButtons();
            Refresh();
            SetStatus("Removed “" + item.Title + "”. Your original note hasn't changed.");
        }

        private void ClearAll()
        {
            if (_notes.Count == 0) return;
            if (_dirty && MessageBox.Show(this, "Clear every note from this collection?\n\nYour notes in iCloud aren't changed. Save the collection first if you want to come back to it.",
                    "Clear collection", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            _notes.Clear();
            Renumber();
            _dirty = false;
            UpdateButtons();
            Refresh();
            SetStatus("Collection cleared.");
        }

        private async Task<bool> SaveProject()
        {
            if (_notes.Count == 0) { SetStatus("Add a note before saving a collection."); return false; }
            var dialog = new SaveFileDialog
            {
                Title = "Save your collection",
                Filter = "NoteChange collection|*.json",
                FileName = FileNames.Safe(_notes.Count == 1 ? _notes[0].Title : "My collection") + ".notechange.json",
                InitialDirectory = Paths.Documents
            };
            if (dialog.ShowDialog(this) != true) return false;
            try
            {
                await File.WriteAllTextAsync(dialog.FileName, Project.Serialize(_notes.Select(n => n.Source)));
                _dirty = false;
                Saved(dialog.FileName, "Collection saved");
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Save collection failed: " + ex.Message);
                SetStatus("Couldn't save the collection there. Try another folder.");
                return false;
            }
        }

        private void UpdateButtons()
        {
            Renumber();
            var item = Selected;
            int i = item == null ? -1 : _notes.IndexOf(item);
            _up.IsEnabled = i > 0;
            _down.IsEnabled = i >= 0 && i < _notes.Count - 1;
            _remove.IsEnabled = i >= 0;
            _saveProject.IsEnabled = _notes.Count > 0;
            _clear.IsEnabled = _notes.Count > 0;
            bool any = Sources().Count > 0;
            foreach (var b in _exports) b.IsEnabled = any;
        }

        // ================================================================ preview

        private FormatOptions Options() => new FormatOptions
        {
            Layout = _layouts.FirstOrDefault(p => p.Value.IsChecked == true).Key,
            TitleMode = _titles.FirstOrDefault(p => p.Value.IsChecked == true).Key,
            CustomTitle = _customTitle.Text,
            LargeText = _largeText.IsChecked == true
        };

        private List<NoteSource> Sources()
        {
            if (_combine.IsChecked == true) return _notes.Select(n => n.Source).ToList();
            var item = Selected ?? (_notes.Count > 0 ? _notes[^1] : null);
            return item == null ? new List<NoteSource>() : new List<NoteSource> { item.Source };
        }

        private void SaveChoices()
        {
            var o = Options();
            _settings.Layout = o.Layout.ToString();
            _settings.TitleMode = o.TitleMode == TitleMode.Custom ? TitleMode.FirstLine.ToString() : o.TitleMode.ToString();
            _settings.LargeText = o.LargeText;
            _settings.Combine = _combine.IsChecked == true;
        }

        /// <summary>Waits for the preview to finish drawing (used by the self-test and screenshots).</summary>
        public Task Rendered => _rendered.Task;

        private async void Refresh()
        {
            if (!_ready) return;
            UpdateButtons();
            var sources = Sources();
            string key = string.Join("|", sources.Select(s => s.GetHashCode()));
            _rendered = new TaskCompletionSource<bool>();

            try { _scroll = key == _shownKey && _preview.CoreWebView2 != null ? double.Parse(await _preview.CoreWebView2.ExecuteScriptAsync("window.scrollY"), System.Globalization.CultureInfo.InvariantCulture) : 0; }
            catch { _scroll = 0; }
            _shownKey = key;

            if (sources.Count == 0)
            {
                ShowWarnings(new List<string>());
                _layoutLabel.Text = "";
                _page?.Show(WelcomePage());
                return;
            }

            try
            {
                var notes = Formatter.FormatAll(sources, Options());
                string heading = notes.Count > 1 ? notes.Count + " notes · each starts on a new page" : "Preview · " + notes[0].LayoutLabel;
                _layoutLabel.Text = notes.Count == 1 && Options().Layout == Layout.Auto ? "Using " + notes[0].LayoutLabel.ToLowerInvariant() : "";
                ShowWarnings(notes.SelectMany(n => n.Warnings).Distinct().ToList());
                _page?.Show(HtmlExporter.PreviewPage(notes, heading));
            }
            catch (NoteException ex)
            {
                SetStatus(ex.Message);
                _rendered.TrySetResult(false);
            }
        }

        private void ShowWarnings(List<string> warnings)
        {
            _warningText.Text = string.Join("\n\n", warnings);
            _warningBox.Visibility = warnings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string WelcomePage()
        {
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang='en-GB'><head><meta charset='utf-8'><style>");
            sb.Append("html{background:#0b0d11}body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;font-family:'Segoe UI Variable Text','Segoe UI',sans-serif;color:#f3f1ec}");
            sb.Append(".wrap{max-width:560px;padding:40px 32px}.eyebrow{color:#ffb547;font-size:12px;font-weight:700;letter-spacing:.14em}");
            sb.Append("h1{font-family:'Segoe UI Variable Display','Segoe UI',sans-serif;font-size:38px;line-height:1.1;margin:12px 0 14px;letter-spacing:-.02em}");
            sb.Append("p{color:#a7abb5;font-size:15.5px;line-height:1.6;margin:0 0 26px}");
            sb.Append("ol{list-style:none;padding:0;margin:0 0 30px;counter-reset:s}li{counter-increment:s;display:flex;gap:14px;align-items:flex-start;margin:0 0 14px;color:#d9d7d2;font-size:14.5px;line-height:1.5}");
            sb.Append("li::before{content:counter(s);flex:0 0 26px;height:26px;border-radius:50%;background:rgba(255,181,71,.16);color:#ffb547;font-weight:700;font-size:13px;display:flex;align-items:center;justify-content:center}");
            sb.Append(".row{display:flex;flex-wrap:wrap;gap:8px}button{font:inherit;font-size:13.5px;font-weight:600;border-radius:9px;padding:9px 14px;cursor:pointer;border:1px solid rgba(255,255,255,.09);background:rgba(255,255,255,.06);color:#f3f1ec}");
            sb.Append("button:hover{background:rgba(255,255,255,.11)}button.primary{background:#ffb547;border-color:#ffb547;color:#1a1206}button:focus-visible{outline:2px solid #ffb547;outline-offset:2px}");
            sb.Append(".small{color:#6e737e;font-size:12.5px;margin:20px 0 0}</style></head><body><div class='wrap'>");
            sb.Append("<div class='eyebrow'>YOUR NOTES, READY TO SHARE</div>");
            sb.Append("<h1>Turn a note into a proper document.</h1>");
            sb.Append("<p>Recipes, chapters and everyday notes, laid out neatly as Word documents, PDFs or emails. Every word stays exactly as you wrote it.</p>");
            sb.Append("<ol><li>Open iCloud Notes and sign in with your Apple Account.</li><li>Choose a note and press <b>Add this note</b>. Add as many as you like.</li><li>Pick a style on the right, then save as Word or PDF.</li></ol>");
            sb.Append("<div class='row'><button class='primary' onclick=\"chrome.webview.postMessage('icloud')\">Open iCloud Notes</button>");
            sb.Append("<button onclick=\"chrome.webview.postMessage('paste')\">Paste a note</button></div>");
            sb.Append("<p class='small'>Or try an example:</p><div class='row'>");
            sb.Append("<button onclick=\"chrome.webview.postMessage('example:recipe')\">A recipe</button>");
            sb.Append("<button onclick=\"chrome.webview.postMessage('example:chapter')\">A book chapter</button>");
            sb.Append("<button onclick=\"chrome.webview.postMessage('example:checklist')\">A checklist</button></div>");
            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        // ================================================================ export

        private Button Export(Button b) { _exports.Add(b); return b; }

        private List<FormattedNote> Current()
        {
            var sources = Sources();
            if (sources.Count == 0) throw new NoteException("Add a note first.");
            return Formatter.FormatAll(sources, Options());
        }

        private bool ConfirmWarnings(IEnumerable<string> warnings)
        {
            var list = warnings.Distinct().ToList();
            if (list.Count == 0) return true;
            return MessageBox.Show(this, string.Join("\n\n", list) + "\n\nSave anyway?", "Check before saving",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes) == MessageBoxResult.Yes;
        }

        private async Task ExportWithDialog(ExportKind kind)
        {
            try
            {
                var notes = Current();
                var warnings = notes.SelectMany(n => n.Warnings).ToList();
                if (kind == ExportKind.Text || kind == ExportKind.Markdown) warnings.AddRange(TextExporter.MarkdownWarnings(Formatter.Flatten(notes)));
                if (!ConfirmWarnings(warnings)) return;

                var (ext, filter) = kind switch
                {
                    ExportKind.Word => (".docx", "Word document|*.docx"),
                    ExportKind.Pdf => (".pdf", "PDF|*.pdf"),
                    ExportKind.Email => (".eml", "Email draft|*.eml"),
                    ExportKind.Markdown => (".md", "Markdown|*.md"),
                    _ => (".txt", "Text|*.txt")
                };
                string name = notes.Count > 1 ? "My collection" : (notes[0].Title.Length > 0 ? notes[0].Title : "My note");
                var dialog = new SaveFileDialog { Title = "Save your document", Filter = filter, FileName = FileNames.Safe(name) + ext, InitialDirectory = Paths.Documents, AddExtension = true };
                if (dialog.ShowDialog(this) != true) return;

                SetStatus("Saving…");
                await ExportTo(kind, notes, dialog.FileName);
                Saved(dialog.FileName, "Saved");
            }
            catch (NoteException ex) { SetStatus(ex.Message); }
            catch (IOException ex)
            {
                Log.Write("Save failed: " + ex.Message);
                SetStatus("Couldn't save there. If the file is open in another app, close it and try again.");
            }
            catch (Exception ex)
            {
                Log.Write("Save failed: " + ex);
                SetStatus("Something went wrong while saving. Please try again.");
            }
        }

        /// <summary>Writes the given notes in the chosen format. No dialogs, so the self-test can use it too.</summary>
        public async Task ExportTo(ExportKind kind, List<FormattedNote> notes, string path)
        {
            switch (kind)
            {
                case ExportKind.Word:
                    await File.WriteAllBytesAsync(path, WordExporter.ToBytes(notes));
                    break;
                case ExportKind.Pdf:
                    await Browser.PrintPdf(this, HtmlExporter.PrintPage(notes), path);
                    break;
                case ExportKind.Email:
                    await File.WriteAllTextAsync(path, EmailExporter.ToEml(Formatter.Flatten(notes)), new UTF8Encoding(false));
                    break;
                case ExportKind.Markdown:
                    await File.WriteAllTextAsync(path, TextExporter.ToMarkdown(Formatter.Flatten(notes)), new UTF8Encoding(false));
                    break;
                default:
                    await File.WriteAllTextAsync(path, TextExporter.ToText(Formatter.Flatten(notes)), new UTF8Encoding(true));
                    break;
            }
        }

        private void CopyForEmail()
        {
            try
            {
                var flat = Formatter.Flatten(Current());
                ClipboardHtml.Set(HtmlExporter.EmailHtml(flat), TextExporter.ToText(flat));
                SetStatus("Copied with its formatting. Paste it into a new email and add a subject.");
            }
            catch (NoteException ex) { SetStatus(ex.Message); }
            catch (Exception ex)
            {
                Log.Write("Copy failed: " + ex.Message);
                SetStatus("Couldn't copy. Another app may be using the clipboard; try again.");
            }
        }

        private void OpenEmail()
        {
            try
            {
                var flat = Formatter.Flatten(Current());
                string link = EmailExporter.MailtoLink(flat);
                if (link == null)
                {
                    ClipboardHtml.Set(HtmlExporter.EmailHtml(flat), TextExporter.ToText(flat));
                    Native.OpenUrl("mailto:");
                    SetStatus("This note is too long to send straight across, so it's been copied. Paste it into the new email.");
                    return;
                }
                Native.OpenUrl(link);
                SetStatus("Your email app should open with the note in a new message.");
            }
            catch (NoteException ex) { SetStatus(ex.Message); }
        }

        private void Saved(string path, string verb)
        {
            _lastFile = path;
            SetStatus(verb + ": " + path);
            _showFile.Visibility = Visibility.Visible;
        }

        private void ShowLastFile()
        {
            if (_lastFile == null || !File.Exists(_lastFile)) return;
            try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + _lastFile + "\"") { UseShellExecute = true }); }
            catch { }
        }

        // ================================================================ updates, sign out, closing

        private async Task CheckUpdates(bool manual)
        {
            if (manual) SetStatus("Checking for updates…");
            bool newer = await Updates.Check();
            if (newer)
            {
                _update.Content = "Update to " + Updates.LatestVersion;
                _update.Visibility = Visibility.Visible;
                SetStatus("NoteChange " + Updates.LatestVersion + " is ready to install.");
            }
            else if (manual) SetStatus("You have the latest version of NoteChange.");
        }

        private async Task InstallUpdate()
        {
            if (_dirty && _notes.Count > 0)
            {
                var r = MessageBox.Show(this, "NoteChange will close to update. Save your collection first?", "Update NoteChange", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return;
                if (r == MessageBoxResult.Yes && !await SaveProject()) return;
            }
            if (await Updates.Install(SetStatus))
            {
                _closingConfirmed = true;
                Application.Current.Shutdown();
            }
        }

        private async Task SignOut()
        {
            if (MessageBox.Show(this, "Sign out of iCloud on this PC?\n\nNoteChange forgets your Apple sign-in. Your collection and saved files stay as they are.",
                    "Sign out of iCloud", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            try
            {
                _icloud?.Close();
                await Browser.SignOut(this);
                SetStatus("Signed out of iCloud.");
            }
            catch (Exception ex)
            {
                Log.Write("Sign out failed: " + ex.Message);
                SetStatus("Couldn't sign out just now. Close NoteChange and try again.");
            }
        }

        private async void OnClosing(object sender, CancelEventArgs e)
        {
            if (!_closingConfirmed && _dirty && _notes.Count > 0 && !ScreenshotMode)
            {
                e.Cancel = true;
                var r = MessageBox.Show(this, "Save your collection before closing?\n\nNotes you've added are only kept while NoteChange is open.",
                    "NoteChange", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return;
                if (r == MessageBoxResult.Yes && !await SaveProject()) return;
                _closingConfirmed = true;
                Close();
                return;
            }
            _settings.Maximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal) { _settings.Width = Width; _settings.Height = Height; }
            SaveChoices();
            if (!ScreenshotMode) _settings.Save();
            try { _icloud?.Close(); } catch { }
        }

        private async void OnKey(object sender, KeyEventArgs e)
        {
            bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
            bool inText = Keyboard.FocusedElement is TextBox;
            if (ctrl && e.Key == Key.V && !inText) { e.Handled = true; PasteNote(); }
            else if (ctrl && e.Key == Key.O) { e.Handled = true; await OpenFiles(); }
            else if (ctrl && e.Key == Key.S && _exports[0].IsEnabled) { e.Handled = true; await ExportWithDialog(ExportKind.Word); }
            else if (ctrl && e.Key == Key.P && _exports[0].IsEnabled) { e.Handled = true; await ExportWithDialog(ExportKind.Pdf); }
            else if (Keyboard.Modifiers == ModifierKeys.Alt && (e.SystemKey == Key.Up || e.SystemKey == Key.Down)) { e.Handled = true; Move(e.SystemKey == Key.Up ? -1 : 1); }
        }

        public void SetStatus(string text)
        {
            _status.Text = text;
            _showFile.Visibility = Visibility.Collapsed;
        }

        // ================================================================ small builders

        private static Button Btn(string text, string style, RoutedEventHandler click)
        {
            var b = new Button { Content = text, Style = (Style)Application.Current.Resources[style] };
            b.Click += click;
            return b;
        }

        private static TextBlock Label(string text) => new TextBlock
        {
            Text = text,
            Foreground = Theme.Faint,
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };

        private RadioButton Segment(string text, string group, string tip) => new RadioButton
        {
            Content = text,
            GroupName = group,
            Style = (Style)Application.Current.Resources["Segment"],
            ToolTip = tip
        };

        private static Grid Row(double gap, params FrameworkElement[] items)
        {
            var g = new Grid();
            for (int i = 0; i < items.Length; i++)
            {
                bool fill = items[i] is Button b && b.Style == (Style)Application.Current.Resources["Secondary"] || items[i] is TextBlock;
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = fill ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
                Grid.SetColumn(items[i], i);
                if (i > 0) items[i].Margin = new Thickness(gap, items[i].Margin.Top, items[i].Margin.Right, items[i].Margin.Bottom);
                g.Children.Add(items[i]);
            }
            return g;
        }

        private static Grid WithPlaceholder(TextBox box, string placeholder)
        {
            var hint = new TextBlock
            {
                Text = placeholder,
                Foreground = Theme.Faint,
                FontSize = 13.5,
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(13, 0, 0, 0)
            };
            var g = new Grid();
            g.Children.Add(box);
            g.Children.Add(hint);
            void Sync() { hint.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; g.Visibility = box.Visibility; hint.Margin = new Thickness(13, box.Margin.Top, 0, 0); }
            box.TextChanged += (s, e) => Sync();
            box.IsVisibleChanged += (s, e) => Sync();
            DependencyPropertyDescriptor.FromProperty(VisibilityProperty, typeof(TextBox)).AddValueChanged(box, (s, e) => Sync());
            Sync();
            return g;
        }

        private static FrameworkElement Logo()
        {
            var image = new Image { Source = App.IconImage, Width = 32, Height = 32 };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            var name = new TextBlock { Text = "NoteChange", FontFamily = Theme.Display, FontSize = 21, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 20) };
            row.Children.Add(image);
            row.Children.Add(name);
            return row;
        }
    }
}
