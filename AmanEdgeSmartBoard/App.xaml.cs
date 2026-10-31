using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace AmanEdgeSmartBoard;

public partial class App : Application
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AMAN EDGE", "SMARTBOARD", "Logs");

    private static readonly string LogFile = Path.Combine(LogDirectory, "startup.log");

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            Log("WPF dispatcher exception", e.Exception);
            MessageBox.Show(
                $"AMAN EDGE SMARTBOARD encountered an error.\n\nDetails have been saved to:\n{LogFile}\n\n{e.Exception.Message}",
                "AMAN EDGE SMARTBOARD — Application error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Log("Unhandled application exception", ex);
            else Log("Unhandled application exception", new Exception(e.ExceptionObject?.ToString()));
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("Application startup requested.");
        try
        {
            base.OnStartup(e);
            Log("WPF startup completed.");
        }
        catch (Exception ex)
        {
            Log("Application startup failed", ex);
            MessageBox.Show(
                $"AMAN EDGE SMARTBOARD could not start.\n\nA diagnostic log was saved to:\n{LogFile}\n\n{ex.Message}",
                "AMAN EDGE SMARTBOARD — Startup failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void Log(string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            if (exception != null) entry += Environment.NewLine + exception + Environment.NewLine;
            File.AppendAllText(LogFile, entry + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Logging must never prevent the application from starting.
        }
    }
}
