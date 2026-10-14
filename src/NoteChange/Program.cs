using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoteChange
{
    public static class App
    {
        private static ImageSource _icon;

        public static ImageSource IconImage => _icon ??= LoadIcon();

        private static ImageSource LoadIcon()
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri("pack://application:,,,/Assets/notechange-256.png", UriKind.Absolute);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }
    }

    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            Theme.Load(app);
            app.DispatcherUnhandledException += (s, e) =>
            {
                Log.Write("Unexpected error: " + e.Exception);
                e.Handled = true;
                if (SelfTest.Running) { SelfTest.Fail("Unhandled: " + e.Exception.Message); return; }
                MessageBox.Show("Something went wrong, but your notes are still here.\n\n" + e.Exception.Message, "NoteChange", MessageBoxButton.OK, MessageBoxImage.Warning);
            };
            TaskScheduler.UnobservedTaskException += (s, e) => { Log.Write("Background error: " + e.Exception); e.SetObserved(); };

            int code = 0;
            if (args.Length > 0 && (args[0] == "--self-test" || args[0] == "--screenshots"))
            {
                string output = args.Length > 1 ? args[1] : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NoteChange-self-test");
                app.Startup += async (s, e) => { code = await SelfTest.Run(output, args[0] == "--screenshots"); app.Shutdown(code); };
                app.Run();
                return code;
            }

            var settings = Settings.Load();
            var window = new MainWindow(settings);
            app.MainWindow = window;
            window.Show();
            foreach (var arg in args)
            {
                if (System.IO.File.Exists(arg)) window.Dispatcher.InvokeAsync(async () => await window.OpenPath(arg));
            }
            return app.Run();
        }
    }
}
