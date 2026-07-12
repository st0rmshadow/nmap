using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.Models;
using Zenmap.Windows.Services;
using Zenmap.Windows.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace Zenmap.Windows.Views;

public sealed partial class ComparePage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;
    private readonly MainWindow _window;

    public ComparePage(ZenmapAppState state, MainWindow window)
    {
        InitializeComponent();
        _state = state;
        _window = window;
        Refresh();
    }

    public void Refresh()
    {
        var scans = _state.ScanHistoryStore.SavedScans;
        BaselineCombo.ItemsSource = scans;
        ComparisonCombo.ItemsSource = scans;
        BaselineCombo.DisplayMemberPath = nameof(SavedScan.Title);
        ComparisonCombo.DisplayMemberPath = nameof(SavedScan.Title);
        if (scans.Count > 0)
        {
            BaselineCombo.SelectedIndex = Math.Min(0, scans.Count - 1);
            ComparisonCombo.SelectedIndex = Math.Min(1, scans.Count - 1);
        }

        UpdateReport();
    }

    private void CompareSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateReport();

    private void UpdateReport()
    {
        if (BaselineCombo.SelectedItem is not SavedScan baseline ||
            ComparisonCombo.SelectedItem is not SavedScan comparison)
        {
            ReportBox.Text = "Select two saved scans to compare.";
            return;
        }

        var baselineHosts = XmlParsing.ParseNmapXml(baseline.XmlPath);
        var comparisonHosts = XmlParsing.ParseNmapXml(comparison.XmlPath);
        var result = ScanComparison.CompareScans(baselineHosts, comparisonHosts);
        ReportBox.Text = ScanComparison.ComparisonReportText(baseline, comparison, result);
    }

    private void CopyButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(ReportBox.Text);
        Clipboard.SetContent(package);
    }

    private async void ExportButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var path = await FileDialogService.PickSaveTextAsync(_window, "scan-comparison.txt");
        if (!string.IsNullOrWhiteSpace(path))
        {
            await File.WriteAllTextAsync(path, ReportBox.Text);
        }
    }
}
