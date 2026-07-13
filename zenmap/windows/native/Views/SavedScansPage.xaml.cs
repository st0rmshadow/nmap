using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.Models;
using Zenmap.Windows.Services;
using Zenmap.Windows.ViewModels;

namespace Zenmap.Windows.Views;

public sealed partial class SavedScansPage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;
    private readonly MainWindow _window;

    public SavedScansPage(ZenmapAppState state, MainWindow window)
    {
        InitializeComponent();
        _state = state;
        _window = window;
        PathText.Text = WindowsPaths.SavedScansDirectory;
        Refresh();
    }

    public void Refresh()
    {
        var scans = ResultsFiltering.FilterSavedScans(_state.ScanHistoryStore.SavedScans, FilterBox.Text).ToList();
        ScansList.ItemsSource = scans;
        CountText.Text = $"{scans.Count}";
    }

    private SavedScan? SelectedScan() => ScansList.SelectedItem as SavedScan;

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void ScansList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var scan = SelectedScan();
        NotesBox.Text = scan?.Notes ?? "";
        TagsBox.Text = scan?.Tags ?? "";
        PersistScanButton.IsEnabled = scan?.Ephemeral == true;
        NotesBox.IsEnabled = scan?.Ephemeral != true;
        TagsBox.IsEnabled = scan?.Ephemeral != true;
    }

    private void LoadButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SelectedScan() is { } scan)
        {
            _state.LoadSavedScan(scan);
        }
    }

    private async void OpenXmlButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SelectedScan() is { } scan && File.Exists(scan.XmlPath))
        {
            await global::Windows.System.Launcher.LaunchFileAsync(await global::Windows.Storage.StorageFile.GetFileFromPathAsync(scan.XmlPath));
        }
    }

    private async void ImportXmlButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var path = await FileDialogService.PickOpenXmlAsync(_window);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _state.ImportXmlFile(path);
        }
    }

    private void DeleteButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SelectedScan() is { } scan)
        {
            _state.DeleteSavedScan(scan.Id);
            Refresh();
        }
    }

    private async void ClearButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear saved scans",
            Content = "Delete all saved scans and their XML files?",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _state.ClearSavedScans();
            Refresh();
        }
    }

    private void PersistScanButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SelectedScan() is { } scan && _state.PersistSavedScan(scan.Id))
        {
            Refresh();
        }
    }

    private void SaveMetadataButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SelectedScan() is { } scan)
        {
            _state.UpdateSavedScanMetadata(scan.Id, NotesBox.Text, TagsBox.Text);
            Refresh();
        }
    }
}
