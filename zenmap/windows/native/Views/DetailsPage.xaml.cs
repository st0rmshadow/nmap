using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.Services;
using Zenmap.Windows.ViewModels;

namespace Zenmap.Windows.Views;

public sealed partial class DetailsPage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;

    public DetailsPage(ZenmapAppState state)
    {
        InitializeComponent();
        _state = state;
        Refresh();
    }

    public void Refresh()
    {
        var hosts = _state.Hosts;
        var ports = ResultsFiltering.AllPorts(hosts);
        var open = ports.Count(port => port.State == "open");
        var filtered = ports.Count(port => port.State == "filtered");
        var closed = ports.Count(port => port.State == "closed");

        SummaryText.Text =
            $"Hosts: {hosts.Count}    Ports: {ports.Count}    Open: {open}    Filtered: {filtered}    Closed: {closed}";

        ContextText.Text = string.Join('\n',
        [
            $"Status: {_state.StatusText}",
            $"Command: {_state.LastCommand}",
            $"Exit status: {_state.ExitStatus?.ToString() ?? "n/a"}",
            $"XML: {_state.LastXmlPath}",
            $"Nmap binary: {_state.SettingsStore.Settings.NmapBinary}",
        ]);

        var host = _state.SelectedHost ?? hosts.FirstOrDefault();
        if (host is null)
        {
            PortsList.ItemsSource = Array.Empty<string>();
            return;
        }

        PortsList.ItemsSource = host.Ports
            .OrderBy(port => int.TryParse(port.PortNumber, out var number) ? number : int.MaxValue)
            .Select(port => $"{port.PortNumber}/{port.ProtocolName}  {port.State}  {port.ServiceName}  {port.ServiceSummary}")
            .ToArray();
    }
}
