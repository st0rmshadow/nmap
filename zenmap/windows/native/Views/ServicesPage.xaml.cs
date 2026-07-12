using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.Services;
using Zenmap.Windows.ViewModels;

namespace Zenmap.Windows.Views;

public sealed partial class ServicesPage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;
    private readonly Action<Models.ScannedHost> _onShowDetails;

    public ServicesPage(ZenmapAppState state, Action<Models.ScannedHost> onShowDetails)
    {
        InitializeComponent();
        _state = state;
        _onShowDetails = onShowDetails;
        Refresh();
    }

    public void Refresh()
    {
        ServicesList.ItemsSource = ResultsFiltering.FilterPorts(ResultsFiltering.ServicePorts(_state.Hosts), FilterBox.Text)
            .Select(port => $"{port.HostAddress}  {port.ServiceName}  {port.ServiceSummary}  {port.PortNumber}/{port.ProtocolName}  {port.State}")
            .ToArray();
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();
}
