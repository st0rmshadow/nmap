using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.Models;
using Zenmap.Windows.ViewModels;

namespace Zenmap.Windows.Views;

public sealed partial class SettingsPage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;

    public SettingsPage(ZenmapAppState state)
    {
        InitializeComponent();
        _state = state;
        Refresh();
    }

    public void Refresh()
    {
        var settings = _state.SettingsStore.Settings;
        DefaultTargetBox.Text = settings.DefaultTarget;
        NmapBinaryBox.Text = settings.NmapBinary;
        DefaultProfileCombo.ItemsSource = _state.Profiles;
        DefaultProfileCombo.DisplayMemberPath = nameof(ScanProfile.Name);
        DefaultProfileCombo.SelectedItem = _state.Profiles.FirstOrDefault(profile => profile.Name == settings.DefaultProfileName)
            ?? _state.Profiles.FirstOrDefault();
        AutoStatsSwitch.IsOn = settings.AutoAddStatsEvery;
        AutoVerboseSwitch.IsOn = settings.AutoAddVerbose;
        StatsEveryCombo.SelectedIndex = settings.StatsEveryValue switch
        {
            "5s" => 1,
            "10s" => 2,
            "30s" => 3,
            "60s" => 4,
            _ => 0,
        };
    }

    private void SaveButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var statsEvery = StatsEveryCombo.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString() ?? "1s"
            : "1s";
        var defaultProfile = DefaultProfileCombo.SelectedItem as ScanProfile;
        _state.SaveSettings(new AppSettings
        {
            DefaultTarget = DefaultTargetBox.Text.Trim(),
            NmapBinary = NmapBinaryBox.Text.Trim(),
            DefaultProfileName = defaultProfile?.Name ?? "Quick Scan",
            AutoAddStatsEvery = AutoStatsSwitch.IsOn,
            AutoAddVerbose = AutoVerboseSwitch.IsOn,
            StatsEveryValue = statsEvery,
        });
    }
}
