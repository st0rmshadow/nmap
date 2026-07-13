using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Zenmap.Windows.Models;
using Zenmap.Windows.Services;
using Zenmap.Windows.ViewModels;

namespace Zenmap.Windows.Views;

public sealed partial class PortsPage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;
    private readonly Action<ScannedHost> _onShowDetails;

    public PortsPage(ZenmapAppState state, Action<ScannedHost> onShowDetails)
    {
        InitializeComponent();
        _state = state;
        _onShowDetails = onShowDetails;
        Refresh();
    }

    public void Refresh()
    {
        var ports = ResultsFiltering.FilterPorts(ResultsFiltering.AllPorts(_state.Hosts), FilterBox.Text).ToList();
        PortsList.ItemsSource = ports;
        CountText.Text = $"{ports.Count}";
        EmptyText.Visibility = ports.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void ShowSelectedHostDetails()
    {
        if (PortsList.SelectedItem is not ScannedPort port)
        {
            return;
        }

        var host = _state.Hosts.FirstOrDefault(item => item.Address == port.HostAddress);
        if (host is not null)
        {
            _onShowDetails(host);
        }
    }

    private void PortsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => ShowSelectedHostDetails();

    private void DetailsButton_Click(object sender, RoutedEventArgs e) => ShowSelectedHostDetails();
}
