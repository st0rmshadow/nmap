using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Zenmap.Windows.Models;
using Zenmap.Windows.Services;
using Zenmap.Windows.ViewModels;

namespace Zenmap.Windows.Views;

public sealed partial class HostsPage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;
    private readonly Action<ScannedHost> _onShowDetails;

    public HostsPage(ZenmapAppState state, Action<ScannedHost> onShowDetails)
    {
        InitializeComponent();
        _state = state;
        _onShowDetails = onShowDetails;
        Refresh();
    }

    public void Refresh()
    {
        var query = FilterBox.Text;
        HostsList.ItemsSource = ResultsFiltering.FilterHosts(_state.Hosts, query);
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs args) => Refresh();

    private void HostsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HostsList.SelectedItem is ScannedHost host)
        {
            _state.ShowHostDetails(host);
        }
    }

    private void HostsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (HostsList.SelectedItem is ScannedHost host)
        {
            _onShowDetails(host);
        }
    }
}
