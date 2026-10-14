using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace NoteChange
{
    /// <summary>A short guide: how to bring notes across, what each style does and where your data goes.</summary>
    public sealed class HelpWindow : Window
    {
        public HelpWindow(Window owner)
        {
            Owner = owner;
            Title = "Help · NoteChange";
            Width = 620;
            Height = 700;
            MinWidth = 480;
            MinHeight = 420;
            Background = Theme.Panel;
            Foreground = Theme.Text;
            FontFamily = Theme.Font;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            try { Icon = App.IconImage; } catch { }

            var body = new StackPanel { Margin = new Thickness(32, 28, 32, 28) };
            body.Children.Add(new TextBlock { Text = "Using NoteChange", FontFamily = Theme.Display, FontSize = 26, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
            body.Children.Add(Para("NoteChange lays your notes out as documents. It never rewrites them: words and their order stay exactly as they are."));

            Section(body, "Bring notes across",
                "Press Open iCloud Notes and sign in with your Apple Account. Open a note, then press Add this note. Keep going to build a collection.",
                "Locked notes need unlocking in iCloud first. Notes kept only \"On My iPhone\" aren't in iCloud, so copy them and use Paste note.",
                "You can also paste from anywhere (Ctrl+V), open .txt, .md or .html files, or drag them onto the window.");
            Section(body, "Styles",
                "Automatic picks a style for you. Recipe turns Ingredients, Method and Notes into headings. Chapter uses book typography with indented paragraphs. Document keeps it clean and simple.",
                "Title uses the first line, a title you type, or none at all.");
            Section(body, "Saving and sharing",
                "Save as Word (.docx) or PDF. Copy for email keeps the formatting when you paste into a new message. Email draft saves an .eml file that opens in Outlook or Mail with pictures in place.",
                "Turn on Whole collection as one document to put every note in one file, each on a new page. Save collection keeps your notes in a file you can open again later.");
            Section(body, "Your privacy",
                "Everything happens on this PC. Notes are never sent to NoteChange or anyone else, and there's no AI rewriting.",
                "Your Apple sign-in is handled by Apple's own page and kept in NoteChange's own browser on this PC. Sign out of iCloud removes it.",
                "Pictures, links, checklists and simple tables come across. Other attachments stay in Apple Notes.");
            Section(body, "Shortcuts",
                "Ctrl+V paste a note · Ctrl+O open a file · Ctrl+S save as Word · Ctrl+P save as PDF · Alt+Up / Alt+Down reorder · Delete remove from the collection · Ctrl+Enter add the open iCloud note");

            var guide = new TextBlock { Margin = new Thickness(0, 16, 0, 0), FontSize = 13.5 };
            var link = new Hyperlink(new Run("Full user guide on GitHub")) { Foreground = Theme.Amber };
            link.Click += (s, e) => Native.OpenUrl("https://github.com/bhxuk/notechange/blob/main/docs/USER-GUIDE.md");
            guide.Inlines.Add(link);
            body.Children.Add(guide);

            var close = new Button { Content = "Got it", Style = (Style)Application.Current.Resources["Primary"], HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(22, 9, 22, 9), IsDefault = true, IsCancel = true };
            close.Click += (s, e) => Close();
            var footer = new Border { Padding = new Thickness(24, 12, 24, 16), BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Child = close };

            var root = new DockPanel();
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = root;
            SourceInitialized += (s, e) => Native.StyleTitleBar(this);
        }

        private static TextBlock Para(string text) => new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Theme.Muted,
            FontSize = 14,
            LineHeight = 21,
            Margin = new Thickness(0, 0, 0, 8)
        };

        private static void Section(Panel body, string heading, params string[] paragraphs)
        {
            body.Children.Add(new TextBlock { Text = heading, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Theme.Amber, Margin = new Thickness(0, 18, 0, 6) });
            foreach (var p in paragraphs) body.Children.Add(Para(p));
        }
    }
}
