using System.Text;
using Microsoft.UI.Xaml;

namespace Zenmap.Windows;

public partial class App : Application
{
    private static readonly List<MainWindow> OpenWindows = [];
    private static readonly string CrashLogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "zenmap-native", "startup-crash.log");

    public App()
    {
        UnhandledException += (_, e) =>
        {
            LogCrash("UnhandledException", e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            LogCrash("InitializeComponent", ex);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            OpenNewWindow();
        }
        catch (Exception ex)
        {
            LogCrash("OnLaunched", ex);
            throw;
        }
    }

    public static MainWindow OpenNewWindow()
    {
        var window = new MainWindow();
        OpenWindows.Add(window);
        window.Activate();
        return window;
    }

    public static void UnregisterWindow(MainWindow window)
    {
        OpenWindows.Remove(window);
        if (OpenWindows.Count == 0)
        {
            Current.Exit();
        }
    }

    public static IReadOnlyList<MainWindow> GetOpenWindows() => OpenWindows;

    public static void ExitApplication()
    {
        foreach (var window in OpenWindows.ToArray())
        {
            window.Close();
        }

        Current.Exit();
    }

    private static void LogCrash(string where, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:o}] {where}");
            while (ex != null)
            {
                sb.AppendLine(ex.GetType().FullName);
                sb.AppendLine(ex.Message);
                sb.AppendLine(ex.StackTrace);
                sb.AppendLine("---");
                ex = ex.InnerException;
            }
            File.AppendAllText(CrashLogPath, sb.ToString());
        }
        catch
        {
            // ignore logging failures
        }
    }
}
