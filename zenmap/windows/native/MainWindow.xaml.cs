using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;
using Zenmap.Windows.Models;
using Zenmap.Windows.Services;
using Zenmap.Windows.ViewModels;
using Zenmap.Windows.Views;

namespace Zenmap.Windows;

public sealed partial class MainWindow : Window
{
    private readonly ZenmapAppState _state;
    private readonly Dictionary<string, Page> _pages = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressFormEvents;
    private readonly SolidColorBrush _idleBrush = new(global::Windows.UI.Color.FromArgb(255, 46, 125, 50));
    private readonly SolidColorBrush _activeBrush = new(global::Windows.UI.Color.FromArgb(255, 230, 81, 0));

    public MainWindow()
    {
        InitializeComponent();
        Title = "Zenmap";
        AppWindow.Title = Title;
        AppWindow.Resize(new SizeInt32(1250, 850));
        _state = new ZenmapAppState(DispatcherQueue);
        _state.Changed += RefreshChrome;
        // OutputPage subscribes to OutputAppended itself; avoid a second ListView sync
        // that races with auto-scroll on every chunk / completion.
        _state.SavedScansChanged += () => GetPage<SavedScansPage>()?.Refresh();
        _state.SavedScansChanged += () => GetPage<ComparePage>()?.Refresh();
        _state.SavedScansChanged += RebuildRecentScansMenu;
        _state.ProfilesChanged += () => GetPage<ProfilesPage>()?.Refresh();

        InitializePages();
        PopulateProfiles();
        ApplyFormFromState();
        RebuildRecentScansMenu();
        RefreshChrome();
        // Frame measures page content with infinite height by default; pin each page to the
        // leftover * slot so nested ScrollViewers get a finite viewport.
        ContentHost.SizeChanged += (_, _) => ApplyContentHostHeight();
        // Window-root wheel router: when the pointer is over Output, ChangeView and mark Handled
        // so NavigationView / other ancestors cannot steal the gesture.
        if (Content is UIElement root)
        {
            root.AddHandler(
                UIElement.PointerWheelChangedEvent,
                new PointerEventHandler(Root_PointerWheelChanged),
                handledEventsToo: true);
        }

        NavigateTo("output");
        Closed += (_, _) => App.UnregisterWindow(this);
    }

    private void Root_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            var point = Content is UIElement root
                ? e.GetCurrentPoint(root)
                : e.GetCurrentPoint(null);
            var delta = point.Properties.MouseWheelDelta;
            var output = GetPage<OutputPage>();
            if (output is null || !output.IsPointerOverOutputArea(e))
            {
                return;
            }

            output.ApplyWheelDelta(delta, out _, out _, out _);
            e.Handled = true;
        }
        catch
        {
            // Ignore wheel routing failures; default scrolling may still apply.
        }
    }

    public ZenmapAppState State => _state;

    private void InitializePages()
    {
        _pages["output"] = new OutputPage(_state);
        _pages["hosts"] = new HostsPage(_state, host => { _state.ShowHostDetails(host); NavigateTo("details"); });
        _pages["ports"] = new PortsPage(_state, host => { _state.ShowHostDetails(host); NavigateTo("details"); });
        _pages["services"] = new ServicesPage(_state, host => { _state.ShowHostDetails(host); NavigateTo("details"); });
        _pages["details"] = new DetailsPage(_state);
        _pages["saved"] = new SavedScansPage(_state, this);
        _pages["compare"] = new ComparePage(_state, this);
        _pages["topology"] = new TopologyPage(_state, host => { _state.ShowHostDetails(host); NavigateTo("details"); });
        _pages["profiles"] = new ProfilesPage(_state, this);
        _pages["settings"] = new SettingsPage(_state);
    }

    private T? GetPage<T>() where T : Page =>
        _pages.Values.OfType<T>().FirstOrDefault();

    private void PopulateProfiles()
    {
        ProfileCombo.ItemsSource = _state.Profiles;
        ProfileCombo.DisplayMemberPath = nameof(ScanProfile.Name);
        ProfileCombo.SelectedItem = _state.SelectedProfile;
        ProfileDescriptionText.Text = _state.SelectedProfile.Description;
    }

    private void ApplyFormFromState()
    {
        _suppressFormEvents = true;
        TargetBox.Text = _state.Target;
        ArgumentsBox.Text = _state.Arguments;
        ProfileCombo.SelectedItem = _state.SelectedProfile;
        CommandPreviewText.Text = _state.CommandPreview;
        ProfileDescriptionText.Text = _state.SelectedProfile.Description;
        _suppressFormEvents = false;
    }

    private void SyncFormFromState()
    {
        _suppressFormEvents = true;
        if (!Equals(ProfileCombo.SelectedItem, _state.SelectedProfile))
        {
            ProfileCombo.SelectedItem = _state.SelectedProfile;
        }

        if (ArgumentsBox.Text != _state.Arguments)
        {
            ArgumentsBox.Text = _state.Arguments;
        }

        if (TargetBox.Text != _state.Target)
        {
            TargetBox.Text = _state.Target;
        }

        CommandPreviewText.Text = _state.CommandPreview;
        ProfileDescriptionText.Text = _state.SelectedProfile.Description;
        _suppressFormEvents = false;
    }

    private void RefreshChrome()
    {
        SyncFormFromState();
        StatusText.Text = _state.StatusText;
        HostCountText.Text = $"{_state.Hosts.Count} host{(_state.Hosts.Count == 1 ? "" : "s")}";
        ProgressText.Text = _state.ProgressText;
        ProgressText.Visibility = string.IsNullOrWhiteSpace(_state.ProgressText) ? Visibility.Collapsed : Visibility.Visible;

        var running = _state.IsScanRunning;
        var canScan = !running && !string.IsNullOrWhiteSpace(_state.Target);
        var canSaveXml = !running && !string.IsNullOrWhiteSpace(_state.LastXmlPath) && File.Exists(_state.LastXmlPath);
        ScanButton.IsEnabled = canScan;
        ScanButtonLabel.Text = running ? "Running..." : "Scan";
        StopButton.IsEnabled = running;
        OpenXmlButton.IsEnabled = !running;
        SaveXmlButton.IsEnabled = canSaveXml;
        StatusDot.Fill = running ? _activeBrush : _idleBrush;

        MenuOpenScan.IsEnabled = !running;
        MenuOpenScanInWindow.IsEnabled = !running;
        RecentScansMenu.IsEnabled = !running;
        MenuSaveScan.IsEnabled = canSaveXml;
        MenuSaveAllScans.IsEnabled = !running && _state.ScanHistoryStore.SavedScans.Count > 0;
        MenuPrint.IsEnabled = !string.IsNullOrWhiteSpace(_state.OutputText);
        MenuStartScan.IsEnabled = canScan;
        MenuStopScan.IsEnabled = running;
        MenuClearOutput.IsEnabled = !running;
        MenuClearResults.IsEnabled = !running;
        MenuCopyOutput.IsEnabled = !string.IsNullOrWhiteSpace(_state.OutputText);

        if (_state.ExitStatus is { } exit && !running)
        {
            StatusText.Text = $"{_state.StatusText}  ·  Exit {exit}";
        }

        if (_state.ProgressPercent is { } percent)
        {
            ScanProgressBar.Visibility = Visibility.Visible;
            ScanProgressBar.Value = percent;
            ScanProgressBar.IsIndeterminate = false;
        }
        else
        {
            ScanProgressBar.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            ScanProgressBar.IsIndeterminate = running;
        }

        foreach (var page in _pages.Values)
        {
            if (page is IZenmapPage refreshable)
            {
                refreshable.Refresh();
            }
        }
    }

    private void RebuildRecentScansMenu()
    {
        RecentScansMenu.Items.Clear();
        var scans = _state.ScanHistoryStore.SavedScans.Take(10).ToList();
        if (scans.Count == 0)
        {
            RecentScansMenu.Items.Add(new MenuFlyoutItem { Text = "No Recent Scans", IsEnabled = false });
            return;
        }

        foreach (var scan in scans)
        {
            var item = new MenuFlyoutItem { Text = scan.Title, Tag = scan.Id };
            item.Click += RecentScanItem_Click;
            RecentScansMenu.Items.Add(item);
        }

        RecentScansMenu.Items.Add(new MenuFlyoutSeparator());
        var clearItem = new MenuFlyoutItem { Text = "Clear Recent Scans" };
        clearItem.Click += MenuClearRecentScans_Click;
        RecentScansMenu.Items.Add(clearItem);
    }

    private void NavigateTo(string tag)
    {
        if (_pages.TryGetValue(tag, out var page))
        {
            ContentFrame.Content = page;
            ApplyContentHostHeight();
            foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
            {
                if (item.Tag is string itemTag && string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    NavView.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void ApplyContentHostHeight()
    {
        if (ContentFrame.Content is FrameworkElement page && ContentHost.ActualHeight > 0)
        {
            page.Height = ContentHost.ActualHeight;
            page.MaxHeight = ContentHost.ActualHeight;
            page.VerticalAlignment = VerticalAlignment.Stretch;
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void FormFieldChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFormEvents)
        {
            return;
        }

        _state.Target = TargetBox.Text;
        _state.Arguments = ArgumentsBox.Text;
        CommandPreviewText.Text = _state.CommandPreview;
        ScanButton.IsEnabled = !_state.IsScanRunning && !string.IsNullOrWhiteSpace(_state.Target);
        MenuStartScan.IsEnabled = ScanButton.IsEnabled;
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFormEvents || ProfileCombo.SelectedItem is not ScanProfile profile)
        {
            return;
        }

        _state.SelectProfile(profile);
        _suppressFormEvents = true;
        ArgumentsBox.Text = _state.Arguments;
        CommandPreviewText.Text = _state.CommandPreview;
        ProfileDescriptionText.Text = profile.Description;
        _suppressFormEvents = false;
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e) => await StartScanFromUiAsync();

    private void StopButton_Click(object sender, RoutedEventArgs e) => _state.StopScan();

    private async void OpenXmlButton_Click(object sender, RoutedEventArgs e) => await OpenScanAsync();

    private async void SaveXmlButton_Click(object sender, RoutedEventArgs e) => await SaveCurrentXmlAsync();

    private void SavedScansButton_Click(object sender, RoutedEventArgs e) => NavigateTo("saved");

    private void FindButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("output");
        GetPage<OutputPage>()?.ShowFindBar();
    }

    private async Task StartScanFromUiAsync()
    {
        if (_state.RequiresPrivilegePrompt())
        {
            var dialog = new ContentDialog
            {
                Title = "Administrator privileges required",
                Content = $"{_state.PrivilegeReason()}\n\nZenmap will request Windows UAC elevation to run this scan.",
                PrimaryButtonText = "Continue",
                CloseButtonText = "Cancel",
                XamlRoot = Content.XamlRoot,
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            NavigateTo("output");
            _state.StartScan(allowPrivileged: true);
            return;
        }

        NavigateTo("output");
        _state.StartScan(allowPrivileged: false);
    }

    private async Task OpenScanAsync()
    {
        var path = await FileDialogService.PickOpenXmlAsync(this);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _state.ImportXmlFile(path);
            NavigateTo("output");
        }
    }

    private void MenuNewWindow_Click(object sender, RoutedEventArgs e) => App.OpenNewWindow();

    private async void MenuOpenScan_Click(object sender, RoutedEventArgs e) => await OpenScanAsync();

    private async void RecentScanItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: Guid scanId })
        {
            var scan = _state.ScanHistoryStore.SavedScans.FirstOrDefault(s => s.Id == scanId);
            if (scan is not null)
            {
                _state.LoadSavedScan(scan);
                NavigateTo("output");
            }
        }

        await Task.CompletedTask;
    }

    private async void MenuClearRecentScans_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear recent scans",
            Content = "Delete all saved scans and their XML files?",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot,
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _state.ClearSavedScans();
            RebuildRecentScansMenu();
        }
    }

    private async void MenuSaveScan_Click(object sender, RoutedEventArgs e) => await SaveCurrentXmlAsync();

    private async Task SaveCurrentXmlAsync()
    {
        if (string.IsNullOrWhiteSpace(_state.LastXmlPath) || !File.Exists(_state.LastXmlPath))
        {
            _state.AppendOutputLine("No XML scan result is available to save.");
            NavigateTo("output");
            return;
        }

        var destination = await FileDialogService.PickSaveXmlAsync(this, "nmap-scan.xml");
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        try
        {
            File.Copy(_state.LastXmlPath, destination, overwrite: true);
            _state.AppendOutputLine($"Saved XML to: {destination}");
        }
        catch (Exception ex)
        {
            _state.AppendOutputLine($"Failed to save XML: {ex.Message}");
        }

        NavigateTo("output");
    }

    private async void MenuSaveAllScans_Click(object sender, RoutedEventArgs e)
    {
        if (_state.ScanHistoryStore.SavedScans.Count == 0)
        {
            _state.AppendOutputLine("No saved scans are available to export.");
            NavigateTo("output");
            return;
        }

        var directory = await FileDialogService.PickFolderAsync(this);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var savedCount = 0;
        var failedCount = 0;
        foreach (var scan in _state.ScanHistoryStore.SavedScans)
        {
            try
            {
                if (!File.Exists(scan.XmlPath))
                {
                    failedCount++;
                    continue;
                }

                var destination = Path.Combine(directory, BuildExportFileName(scan));
                File.Copy(scan.XmlPath, destination, overwrite: true);
                savedCount++;
            }
            catch
            {
                failedCount++;
            }
        }

        _state.AppendOutputLine($"Saved {savedCount} scan{(savedCount == 1 ? "" : "s")} to: {directory}");
        if (failedCount > 0)
        {
            _state.AppendOutputLine($"Failed to save {failedCount} scan{(failedCount == 1 ? "" : "s")}.");
        }

        NavigateTo("output");
    }

    private void MenuPrint_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"zenmap-output-{Guid.NewGuid():N}.txt");
            File.WriteAllText(tempPath, string.IsNullOrWhiteSpace(_state.OutputText) ? "(empty)" : _state.OutputText, Encoding.UTF8);
            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true,
                Verb = "print",
            });
            _state.AppendOutputLine($"Sent output to the print system: {tempPath}");
        }
        catch (Exception ex)
        {
            _state.AppendOutputLine($"Print failed: {ex.Message}");
        }

        NavigateTo("output");
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e) => App.ExitApplication();

    private void MenuFindOutput_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("output");
        GetPage<OutputPage>()?.ShowFindBar();
    }

    private void MenuCopyOutput_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("output");
        GetPage<OutputPage>()?.CopyOutputToClipboard();
    }

    private void MenuClearOutput_Click(object sender, RoutedEventArgs e)
    {
        _state.ClearOutput();
        NavigateTo("output");
    }

    private void MenuShowTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string tag })
        {
            NavigateTo(tag);
        }
    }

    private async void MenuStartScan_Click(object sender, RoutedEventArgs e) => await StartScanFromUiAsync();

    private void MenuStopScan_Click(object sender, RoutedEventArgs e) => _state.StopScan();

    private void MenuClearResults_Click(object sender, RoutedEventArgs e)
    {
        _state.ClearResults();
        NavigateTo("output");
    }

    private void MenuCloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private async void MenuOpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string url } && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    private void MenuCopyDiagnostic_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(BuildDiagnosticInfo());
        Clipboard.SetContent(package);
        _state.AppendOutputLine("Copied diagnostic info to clipboard.");
        NavigateTo("output");
    }

    private async void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "7.99";
        var dialog = new ContentDialog
        {
            Title = "About Zenmap",
            Content = $"Zenmap for Windows\nVersion {version}\n\nNative WinUI front end for Nmap.\nhttps://nmap.org/",
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot,
            DefaultButton = ContentDialogButton.Close,
        };
        await dialog.ShowAsync();
    }

    private string BuildDiagnosticInfo()
    {
        var nmapBinary = _state.SettingsStore.Settings.NmapBinary;
        var resolved = NmapPathResolver.ResolveNmapBinary(nmapBinary) ?? nmapBinary;
        var nmapDir = string.IsNullOrWhiteSpace(resolved) ? "unavailable" : NmapPathResolver.ResolveNmapDataDirectory(resolved);
        var nmapVersion = ReadNmapVersion(resolved, nmapDir);
        var os = Environment.OSVersion.VersionString;
        var arch = Environment.Is64BitProcess ? "x64" : "x86";
        var appPath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

        return string.Join(Environment.NewLine,
        [
            "Zenmap Diagnostic Info",
            "",
            "App:",
            $"Path: {appPath}",
            $"Version: {version}",
            "",
            "System:",
            $"Windows: {os}",
            $"Host: {Environment.MachineName}",
            $"Architecture: {arch}",
            "",
            "Nmap Runtime:",
            $"Nmap binary: {resolved}",
            $"NMAPDIR: {nmapDir}",
            "Nmap version:",
            nmapVersion,
            "",
            "Last Scan:",
            $"Command: {(string.IsNullOrWhiteSpace(_state.LastCommand) ? "none" : _state.LastCommand)}",
            $"XML: {(string.IsNullOrWhiteSpace(_state.LastXmlPath) ? "none" : _state.LastXmlPath)}",
            $"Exit status: {_state.ExitStatus?.ToString() ?? "none"}",
            $"Status: {_state.StatusText}",
            $"Hosts parsed: {_state.Hosts.Count}",
            $"Ports parsed: {_state.Hosts.Sum(host => host.Ports.Count)}",
            "",
            "Privilege:",
            $"Currently running: {(_state.IsScanRunning ? "yes" : "no")}",
        ]);
    }

    private static string ReadNmapVersion(string? nmapPath, string nmapDir)
    {
        if (string.IsNullOrWhiteSpace(nmapPath) || !File.Exists(nmapPath))
        {
            return "unavailable";
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = nmapPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (!string.IsNullOrWhiteSpace(nmapDir))
            {
                startInfo.Environment["NMAPDIR"] = nmapDir;
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return "unavailable";
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(5000);
            return string.IsNullOrWhiteSpace(output) ? "unavailable" : output.Trim();
        }
        catch (Exception ex)
        {
            return $"unavailable ({ex.Message})";
        }
    }

    private static string BuildExportFileName(SavedScan scan)
    {
        var timestamp = scan.ScannedAt.ToString("yyyy-MM-ddTHH-mm-ss");
        var safeTitle = string.Join("_", scan.Title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(safeTitle))
        {
            safeTitle = "nmap-scan";
        }

        return $"{timestamp}-{safeTitle}.xml";
    }
}
