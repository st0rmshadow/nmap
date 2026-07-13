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

        HostsMetric.Text = hosts.Count.ToString();
        PortsMetric.Text = ports.Count.ToString();
        OpenMetric.Text = open.ToString();
        FilteredMetric.Text = filtered.ToString();
        ClosedMetric.Text = closed.ToString();

        StatusLine.Text = $"Status: {_state.StatusText}";
        CommandLine.Text = $"Command: {_state.LastCommand}";
        ExitLine.Text = $"Exit status: {_state.ExitStatus?.ToString() ?? "n/a"}";
        XmlLine.Text = $"XML: {_state.LastXmlPath}";
        BinaryLine.Text = $"Nmap binary: {_state.SettingsStore.Settings.NmapBinary}";

        var host = _state.SelectedHost ?? hosts.FirstOrDefault();
        if (host is null)
        {
            HostHeaderText.Text = "Select a host in the Hosts tab to inspect ports.";
            PortsList.ItemsSource = null;
            return;
        }

        HostHeaderText.Text = $"{host.DisplayName}  ·  {host.Address}  ·  {host.Status}";
        PortsList.ItemsSource = host.Ports
            .OrderBy(port => int.TryParse(port.PortNumber, out var number) ? number : int.MaxValue)
            .ToList();
    }
}
