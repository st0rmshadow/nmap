using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.Services;
using Zenmap.Windows.ViewModels;
using Zenmap.Windows.Views;

namespace Zenmap.Windows;

public sealed partial class MainWindow : Window
{
    private readonly ZenmapAppState _state;
    private readonly Dictionary<string, Page> _pages = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressFormEvents;

    public MainWindow()
    {
        InitializeComponent();
        _state = new ZenmapAppState(DispatcherQueue);
        _state.Changed += RefreshChrome;
        _state.OutputAppended += _ => GetPage<OutputPage>()?.Refresh();
        _state.SavedScansChanged += () => GetPage<SavedScansPage>()?.Refresh();
        _state.SavedScansChanged += () => GetPage<ComparePage>()?.Refresh();
        _state.ProfilesChanged += () => GetPage<ProfilesPage>()?.Refresh();

        InitializePages();
        PopulateProfiles();
        ApplyFormFromState();
        RefreshChrome();
        NavigateTo("output");
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
        ProfileCombo.DisplayMemberPath = nameof(Models.ScanProfile.Name);
        ProfileCombo.SelectedItem = _state.SelectedProfile;
    }

    private void ApplyFormFromState()
    {
        _suppressFormEvents = true;
        TargetBox.Text = _state.Target;
        ArgumentsBox.Text = _state.Arguments;
        ProfileCombo.SelectedItem = _state.SelectedProfile;
        CommandPreviewText.Text = _state.CommandPreview;
        _suppressFormEvents = false;
    }

    private void RefreshChrome()
    {
        StatusText.Text = _state.StatusText;
        HostCountText.Text = $"{_state.Hosts.Count} hosts";
        ProgressText.Text = _state.ProgressText;
        ScanButton.IsEnabled = !_state.IsScanRunning;
        StopButton.IsEnabled = _state.IsScanRunning;

        if (_state.ProgressPercent is { } percent)
        {
            ScanProgressBar.Visibility = Visibility.Visible;
            ScanProgressBar.Value = percent;
            ScanProgressBar.IsIndeterminate = false;
        }
        else
        {
            ScanProgressBar.Visibility = _state.IsScanRunning ? Visibility.Visible : Visibility.Collapsed;
            ScanProgressBar.IsIndeterminate = _state.IsScanRunning;
        }

        foreach (var page in _pages.Values)
        {
            if (page is IZenmapPage refreshable)
            {
                refreshable.Refresh();
            }
        }
    }

    private void NavigateTo(string tag)
    {
        if (_pages.TryGetValue(tag, out var page))
        {
            ContentFrame.Content = page;
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            NavigateTo("settings");
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
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFormEvents || ProfileCombo.SelectedItem is not Models.ScanProfile profile)
        {
            return;
        }

        _state.SelectProfile(profile);
        _suppressFormEvents = true;
        ArgumentsBox.Text = _state.Arguments;
        CommandPreviewText.Text = _state.CommandPreview;
        _suppressFormEvents = false;
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
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

    private void StopButton_Click(object sender, RoutedEventArgs e) => _state.StopScan();

    private async void OpenXmlButton_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogService.PickOpenXmlAsync(this);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _state.ImportXmlFile(path);
            NavigateTo("output");
        }
    }
}
