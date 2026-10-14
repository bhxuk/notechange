using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using NoteChange.Core;

namespace NoteChange
{
    /// <summary>
    /// The two embedded browsers. The preview browser only ever shows pages NoteChange builds in memory.
    /// The iCloud browser keeps its own sign-in, separate from Edge and from the preview.
    /// </summary>
    public static class Browser
    {
        private static Task<CoreWebView2Environment> _preview;
        private static Task<CoreWebView2Environment> _icloud;

        public static Task<CoreWebView2Environment> PreviewEnvironment() =>
            _preview ??= CoreWebView2Environment.CreateAsync(null, Paths.PreviewBrowser, new CoreWebView2EnvironmentOptions { Language = "en-GB" });

        public static Task<CoreWebView2Environment> ICloudEnvironment() =>
            _icloud ??= CoreWebView2Environment.CreateAsync(null, Paths.ICloudBrowser, new CoreWebView2EnvironmentOptions { Language = "en-GB" });

        public static string RuntimeVersion()
        {
            try { return CoreWebView2Environment.GetAvailableBrowserVersionString(); }
            catch { return null; }
        }

        public const string MissingRuntime =
            "NoteChange needs the Microsoft Edge WebView2 Runtime, which comes with Windows 11 and most Windows 10 PCs. " +
            "It isn't installed on this PC. Download the \"Evergreen Bootstrapper\" from Microsoft, run it, then open NoteChange again.";

        public const string RuntimeUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

        /// <summary>Prints notes to an A4 PDF using an invisible browser.</summary>
        public static async Task PrintPdf(Window owner, string html, string path)
        {
            var env = await PreviewEnvironment();
            var hwnd = new WindowInteropHelper(owner).EnsureHandle();
            var controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
            try
            {
                controller.IsVisible = false;
                controller.Bounds = new System.Drawing.Rectangle(0, 0, 820, 1100);
                var web = controller.CoreWebView2;
                web.Settings.IsScriptEnabled = false;
                web.Settings.AreDevToolsEnabled = false;
                var page = new PageHost(web, env);
                var loaded = new TaskCompletionSource<bool>();
                web.NavigationCompleted += (s, e) => loaded.TrySetResult(e.IsSuccess);
                page.Show(html);
                if (await Task.WhenAny(loaded.Task, Task.Delay(30000)) != loaded.Task || !loaded.Task.Result)
                    throw new NoteException("Couldn't prepare the PDF. Please try again.");

                var settings = env.CreatePrintSettings();
                settings.PageWidth = 8.27;
                settings.PageHeight = 11.69;
                settings.MarginTop = 0.79;
                settings.MarginBottom = 0.79;
                settings.MarginLeft = 0.83;
                settings.MarginRight = 0.83;
                settings.ShouldPrintBackgrounds = true;
                settings.ShouldPrintHeaderAndFooter = false;
                settings.ShouldPrintSelectionOnly = false;
                if (!await web.PrintToPdfAsync(path, settings))
                    throw new NoteException("Couldn't save the PDF. Check that the folder exists and the file isn't open somewhere else.");
            }
            finally
            {
                controller.Close();
            }
        }

        /// <summary>Removes the iCloud sign-in, cookies and cache from this PC.</summary>
        public static async Task SignOut(Window owner)
        {
            var env = await ICloudEnvironment();
            var hwnd = new WindowInteropHelper(owner).EnsureHandle();
            var controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
            try
            {
                controller.IsVisible = false;
                await controller.CoreWebView2.Profile.ClearBrowsingDataAsync();
            }
            finally
            {
                controller.Close();
            }
        }
    }

    /// <summary>Serves one page from memory at https://notechange.local/ so nothing is written to disk.</summary>
    public sealed class PageHost
    {
        public const string Origin = "https://notechange.local/";
        private readonly CoreWebView2 _web;
        private readonly CoreWebView2Environment _env;
        private string _html = "<!doctype html><html><body style='background:#0b0d11'></body></html>";
        private int _version;

        public PageHost(CoreWebView2 web, CoreWebView2Environment env)
        {
            _web = web;
            _env = env;
            web.AddWebResourceRequestedFilter(Origin + "*", CoreWebView2WebResourceContext.All);
            web.WebResourceRequested += OnRequest;
        }

        public void Show(string html)
        {
            _html = html ?? "";
            _web.Navigate(Origin + "page?v=" + (++_version));
        }

        public static bool IsOwnPage(string uri) => uri != null && uri.StartsWith(Origin, StringComparison.OrdinalIgnoreCase);

        private void OnRequest(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var uri = new Uri(e.Request.Uri);
            if (uri.AbsolutePath == "/page")
            {
                var bytes = Encoding.UTF8.GetBytes(_html);
                e.Response = _env.CreateWebResourceResponse(new MemoryStream(bytes), 200, "OK",
                    "Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store");
            }
            else
            {
                e.Response = _env.CreateWebResourceResponse(new MemoryStream(), 404, "Not Found", "Content-Type: text/plain");
            }
        }
    }
}
