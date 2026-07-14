using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.Services;

namespace Zenmap.Windows;

public partial class App : Application
{
    private static readonly List<MainWindow> OpenWindows = [];
    private static readonly string CrashLogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "zenmap-native", "startup-crash.log");

    public static SettingsStore SettingsStore { get; } = CreateSettingsStore();
    public static ProfileStore ProfileStore { get; } = CreateProfileStore();
    public static ScanHistoryStore ScanHistoryStore { get; } = CreateScanHistoryStore();

    public App()
    {
        UnhandledException += (_, e) =>
        {
            LogCrash("UnhandledException", e.Exception);
            var window = OpenWindows.FirstOrDefault();
            if (window?.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await ShowFatalErrorDialogAsync(window, e.Exception);
                }
                finally
                {
                    ExitApplication();
                }
            }) == true)
            {
                e.Handled = true;
                return;
            }

            e.Handled = false;
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
            ScanHistoryStore.CleanupEphemeralScans();
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

    private static SettingsStore CreateSettingsStore()
    {
        WindowsPaths.EnsureConfigDirectories();
        return new SettingsStore();
    }

    private static ProfileStore CreateProfileStore() => new();

    private static ScanHistoryStore CreateScanHistoryStore() => new();

    private static async Task ShowFatalErrorDialogAsync(MainWindow window, Exception? ex)
    {
        var message = ex?.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Zenmap encountered an unexpected error and must close.";
        }

        if (window.Content?.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Zenmap Error",
            Content = message,
            CloseButtonText = "Exit",
            XamlRoot = xamlRoot,
            DefaultButton = ContentDialogButton.Close,
        };
        await dialog.ShowAsync();
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
