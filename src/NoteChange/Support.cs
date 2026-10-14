using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace NoteChange
{
    /// <summary>Where NoteChange keeps its files on this PC.</summary>
    public static class Paths
    {
        public static string Local => Ensure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoteChange"));
        public static string Roaming => Ensure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoteChange"));
        public static string PreviewBrowser => Ensure(Path.Combine(Local, "WebView2", "Preview"));
        public static string ICloudBrowser => Ensure(Path.Combine(Local, "WebView2", "iCloud"));
        public static string Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        private static string Ensure(string p) { Directory.CreateDirectory(p); return p; }
    }

    public static class Log
    {
        private static readonly object Gate = new object();

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    string file = Path.Combine(Paths.Local, "notechange.log");
                    if (File.Exists(file) && new FileInfo(file).Length > 512 * 1024) File.Delete(file);
                    File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    /// <summary>Remembered choices: style, title, text size and window size.</summary>
    public sealed class Settings
    {
        public string Layout { get; set; } = "Auto";
        public string TitleMode { get; set; } = "FirstLine";
        public bool LargeText { get; set; } = true;
        public bool Combine { get; set; }
        public bool CheckForUpdates { get; set; } = true;
        public double Width { get; set; }
        public double Height { get; set; }
        public bool Maximized { get; set; }

        private static string SettingsFile => Path.Combine(Paths.Roaming, "settings.json");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsFile)) ?? new Settings();
            }
            catch (Exception ex) { Log.Write("Settings unreadable: " + ex.Message); }
            return new Settings();
        }

        public void Save()
        {
            try { File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
            catch (Exception ex) { Log.Write("Settings not saved: " + ex.Message); }
        }
    }

    /// <summary>Checks GitHub for a newer release and installs it with the Setup.exe.</summary>
    public static class Updates
    {
        public const string Repo = "bhxuk/notechange";
        public const string SetupName = "NoteChange-Setup.exe";
        public static string LatestVersion { get; private set; }
        public static string LatestSetupUrl { get; private set; }

        public static Version Current => typeof(Updates).Assembly.GetName().Version ?? new Version(1, 0, 0);
        public static string CurrentText => Current.Major + "." + Current.Minor + "." + Math.Max(0, Current.Build);

        public static async Task<bool> Check()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("NoteChange/" + CurrentText);
                using var doc = JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/" + Repo + "/releases/latest"));
                string tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
                LatestSetupUrl = null;
                if (doc.RootElement.TryGetProperty("assets", out var assets))
                    foreach (var a in assets.EnumerateArray())
                        if ((a.GetProperty("name").GetString() ?? "").Equals(SetupName, StringComparison.OrdinalIgnoreCase))
                            LatestSetupUrl = a.GetProperty("browser_download_url").GetString();
                LatestVersion = tag.TrimStart('v', 'V');
                return LatestSetupUrl != null && Version.TryParse(LatestVersion, out var v)
                    && v > new Version(Current.Major, Current.Minor, Math.Max(0, Current.Build));
            }
            catch (Exception ex)
            {
                Log.Write("Update check failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>Downloads the newest Setup.exe and runs it quietly. Setup closes NoteChange, updates it and opens it again.</summary>
        public static async Task<bool> Install(Action<string> status)
        {
            try
            {
                if (string.IsNullOrEmpty(LatestSetupUrl) && !await Check()) return false;
                status("Downloading NoteChange " + LatestVersion + "…");
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("NoteChange/" + CurrentText);
                byte[] bytes = await http.GetByteArrayAsync(LatestSetupUrl);
                string dir = Path.Combine(Path.GetTempPath(), "NoteChange");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "NoteChange-Setup-" + LatestVersion + ".exe");
                await File.WriteAllBytesAsync(path, bytes);
                status("Installing the update…");
                Process.Start(new ProcessStartInfo(path, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Update failed: " + ex.Message);
                status("Couldn't install the update. Try again later, or download it from GitHub.");
                return false;
            }
        }
    }

    /// <summary>Puts formatted text on the clipboard so it pastes into email with its formatting.</summary>
    public static class ClipboardHtml
    {
        public static void Set(string htmlFragment, string plainText)
        {
            var data = new DataObject();
            data.SetData(DataFormats.Html, Build(htmlFragment));
            data.SetData(DataFormats.UnicodeText, plainText);
            data.SetData(DataFormats.Text, plainText);
            Clipboard.SetDataObject(data, true);
        }

        /// <summary>Builds the CF_HTML clipboard format. Offsets count UTF-8 bytes.</summary>
        public static string Build(string fragment)
        {
            const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
            string pre = "<html><head><meta charset=\"utf-8\"></head><body><!--StartFragment-->";
            string post = "<!--EndFragment--></body></html>";
            int headerLength = Encoding.UTF8.GetByteCount(string.Format(header, 0, 0, 0, 0));
            int startHtml = headerLength;
            int startFragment = startHtml + Encoding.UTF8.GetByteCount(pre);
            int endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
            int endHtml = endFragment + Encoding.UTF8.GetByteCount(post);
            return string.Format(header, startHtml, endHtml, startFragment, endFragment) + pre + fragment + post;
        }

        /// <summary>Reads the fragment out of CF_HTML clipboard data.</summary>
        public static string Fragment(string cfHtml)
        {
            if (string.IsNullOrEmpty(cfHtml)) return null;
            int s = cfHtml.IndexOf("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase);
            int e = cfHtml.IndexOf("<!--EndFragment-->", StringComparison.OrdinalIgnoreCase);
            if (s >= 0 && e > s) return cfHtml.Substring(s + 20, e - s - 20);
            int body = cfHtml.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
            return body >= 0 ? cfHtml.Substring(body) : cfHtml;
        }
    }

    internal static class Native
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>Dark title bar and a caption colour that matches the window.</summary>
        public static void StyleTitleBar(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                int on = 1;
                if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0) DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
                int caption = 0x00151110; // COLORREF for #101115 (BGR)
                DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
                int corners = 2;
                DwmSetWindowAttribute(hwnd, 33, ref corners, sizeof(int));
            }
            catch { }
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Write("Couldn't open " + url + ": " + ex.Message); }
        }
    }
}
