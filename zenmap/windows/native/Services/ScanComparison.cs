using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public sealed class ScanComparisonResult
{
    public IReadOnlyList<string> NewHosts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingHosts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NewOpenPorts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ClosedPorts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ChangedServices { get; init; } = Array.Empty<string>();
}

public static class ScanComparison
{
    public static ScanComparisonResult CompareScans(
        IReadOnlyList<ScannedHost> baseline,
        IReadOnlyList<ScannedHost> comparison)
    {
        var baselineMap = baseline.Where(host => !string.IsNullOrWhiteSpace(host.Address))
            .ToDictionary(host => host.Address);
        var comparisonMap = comparison.Where(host => !string.IsNullOrWhiteSpace(host.Address))
            .ToDictionary(host => host.Address);

        var baselineAddresses = baselineMap.Keys.ToHashSet();
        var comparisonAddresses = comparisonMap.Keys.ToHashSet();

        var newHosts = comparisonAddresses.Except(baselineAddresses).OrderBy(address => address).ToArray();
        var missingHosts = baselineAddresses.Except(comparisonAddresses).OrderBy(address => address).ToArray();

        var newOpenPorts = new List<string>();
        var closedPorts = new List<string>();
        var changedServices = new List<string>();

        foreach (var hostAddress in baselineAddresses.Intersect(comparisonAddresses).OrderBy(address => address))
        {
            var baselinePorts = OpenPortMap(baselineMap[hostAddress]);
            var comparisonPorts = OpenPortMap(comparisonMap[hostAddress]);
            var baselineKeys = baselinePorts.Keys.ToHashSet();
            var comparisonKeys = comparisonPorts.Keys.ToHashSet();

            foreach (var key in comparisonKeys.Except(baselineKeys).OrderBy(key => key))
            {
                var port = comparisonPorts[key];
                newOpenPorts.Add($"{hostAddress} {port.ProtocolName}/{port.PortNumber} {PortServiceDescription(port)}");
            }

            foreach (var key in baselineKeys.Except(comparisonKeys).OrderBy(key => key))
            {
                var port = baselinePorts[key];
                closedPorts.Add($"{hostAddress} {port.ProtocolName}/{port.PortNumber} {PortServiceDescription(port)}");
            }

            foreach (var key in baselineKeys.Intersect(comparisonKeys).OrderBy(key => key))
            {
                var baselineService = PortServiceDescription(baselinePorts[key]);
                var comparisonService = PortServiceDescription(comparisonPorts[key]);
                if (baselineService != comparisonService)
                {
                    var port = comparisonPorts[key];
                    changedServices.Add(
                        $"{hostAddress} {port.ProtocolName}/{port.PortNumber}: {baselineService} -> {comparisonService}");
                }
            }
        }

        return new ScanComparisonResult
        {
            NewHosts = newHosts,
            MissingHosts = missingHosts,
            NewOpenPorts = newOpenPorts,
            ClosedPorts = closedPorts,
            ChangedServices = changedServices,
        };
    }

    public static string ScanLabel(SavedScan scan) =>
        $"{scan.ScannedAt:yyyy-MM-dd HH:mm} - {scan.Title}";

    public static string ComparisonReportText(
        SavedScan baselineScan,
        SavedScan comparisonScan,
        ScanComparisonResult comparison)
    {
        static string Section(string title, IReadOnlyList<string> rows) =>
            rows.Count == 0 ? $"{title}:\nNo changes" : $"{title}:\n{string.Join('\n', rows.Select(row => $"- {row}"))}";

        var ndiffLines = new List<string>();
        ndiffLines.AddRange(comparison.NewHosts.Select(row => $"+ Host added: {row}"));
        ndiffLines.AddRange(comparison.MissingHosts.Select(row => $"- Host removed: {row}"));
        ndiffLines.AddRange(comparison.NewOpenPorts.Select(row => $"+ Open port: {row}"));
        ndiffLines.AddRange(comparison.ClosedPorts.Select(row => $"- Open port removed or closed: {row}"));
        ndiffLines.AddRange(comparison.ChangedServices.Select(row => $"~ Service changed: {row}"));
        if (ndiffLines.Count == 0)
        {
            ndiffLines.Add("No differences detected.");
        }

        return string.Join('\n',
        [
            "Nmap Scan Comparison Report",
            "",
            "Baseline Scan:",
            $"  {ScanLabel(baselineScan)}",
            $"  Command: {baselineScan.Command}",
            $"  XML: {baselineScan.XmlPath}",
            "",
            "Comparison Scan:",
            $"  {ScanLabel(comparisonScan)}",
            $"  Command: {comparisonScan.Command}",
            $"  XML: {comparisonScan.XmlPath}",
            "",
            "Summary:",
            $"  New Hosts: {comparison.NewHosts.Count}",
            $"  Missing Hosts: {comparison.MissingHosts.Count}",
            $"  New Open Ports: {comparison.NewOpenPorts.Count}",
            $"  Closed Ports: {comparison.ClosedPorts.Count}",
            $"  Service Changes: {comparison.ChangedServices.Count}",
            "",
            "Ndiff-style Changes:",
            ..ndiffLines,
            "",
            Section("New Hosts", comparison.NewHosts),
            "",
            Section("Missing Hosts", comparison.MissingHosts),
            "",
            Section("New Open Ports", comparison.NewOpenPorts),
            "",
            Section("Closed Ports", comparison.ClosedPorts),
            "",
            Section("Changed Services", comparison.ChangedServices),
        ]);
    }

    private static Dictionary<string, ScannedPort> OpenPortMap(ScannedHost host) =>
        host.Ports
            .Where(port => port.State == "open")
            .ToDictionary(port => $"{port.ProtocolName}/{port.PortNumber}");

    private static string PortServiceDescription(ScannedPort port)
    {
        var parts = new[] { port.ServiceName, port.Product, port.Version, port.ExtraInfo }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var description = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(description) ? "(no service details)" : description;
    }
}
