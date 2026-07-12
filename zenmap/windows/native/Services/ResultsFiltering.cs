using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public static class ResultsFiltering
{
    public static string NormalizeFilterText(string text) => text.Trim().ToLowerInvariant();

    public static bool HostMatchesFilter(ScannedHost host, string query)
    {
        var hostText = string.Join(' ',
            host.Address,
            host.Hostname,
            host.Status,
            host.OpenPortCount.ToString()).ToLowerInvariant();
        if (hostText.Contains(query, StringComparison.Ordinal))
        {
            return true;
        }

        return host.Ports.Any(port => PortMatchesFilter(port, query));
    }

    public static bool PortMatchesFilter(ScannedPort port, string query)
    {
        var haystack = string.Join(' ',
            port.HostAddress,
            port.ProtocolName,
            port.PortNumber,
            port.State,
            port.ServiceName,
            port.Product,
            port.Version,
            port.ExtraInfo,
            port.ServiceSummary).ToLowerInvariant();
        return haystack.Contains(query, StringComparison.Ordinal);
    }

    public static bool ProfileMatchesFilter(ScanProfile profile, string query)
    {
        var profileType = profile.IsBuiltIn ? "built-in builtin default" : "custom user";
        var haystack = string.Join(' ',
            profile.Name,
            profile.Arguments,
            profile.Description,
            profileType).ToLowerInvariant();
        return haystack.Contains(query, StringComparison.Ordinal);
    }

    public static bool SavedScanMatchesFilter(SavedScan scan, string query)
    {
        var dateText = scan.ScannedAt.ToString("yyyy-MM-dd HH:mm");
        var haystack = string.Join(' ',
            scan.Title,
            scan.Command,
            scan.XmlPath,
            scan.Notes,
            scan.Tags,
            dateText,
            scan.HostCount.ToString(),
            scan.PortCount.ToString()).ToLowerInvariant();
        return haystack.Contains(query, StringComparison.Ordinal);
    }

    public static IReadOnlyList<ScannedPort> AllPorts(IEnumerable<ScannedHost> hosts) =>
        hosts.SelectMany(host => host.Ports).ToArray();

    public static IReadOnlyList<ScannedPort> ServicePorts(IEnumerable<ScannedHost> hosts) =>
        AllPorts(hosts).Where(port => !string.IsNullOrWhiteSpace(port.ServiceName) || !string.IsNullOrWhiteSpace(port.ServiceSummary)).ToArray();

    public static IReadOnlyList<ScannedHost> FilterHosts(IEnumerable<ScannedHost> hosts, string query)
    {
        var normalized = NormalizeFilterText(query);
        return string.IsNullOrEmpty(normalized)
            ? hosts.ToArray()
            : hosts.Where(host => HostMatchesFilter(host, normalized)).ToArray();
    }

    public static IReadOnlyList<ScannedPort> FilterPorts(IEnumerable<ScannedPort> ports, string query)
    {
        var normalized = NormalizeFilterText(query);
        return string.IsNullOrEmpty(normalized)
            ? ports.ToArray()
            : ports.Where(port => PortMatchesFilter(port, normalized)).ToArray();
    }

    public static IReadOnlyList<ScanProfile> FilterProfiles(IEnumerable<ScanProfile> profiles, string query)
    {
        var normalized = NormalizeFilterText(query);
        return string.IsNullOrEmpty(normalized)
            ? profiles.ToArray()
            : profiles.Where(profile => ProfileMatchesFilter(profile, normalized)).ToArray();
    }

    public static IReadOnlyList<SavedScan> FilterSavedScans(IEnumerable<SavedScan> scans, string query)
    {
        var normalized = NormalizeFilterText(query);
        return string.IsNullOrEmpty(normalized)
            ? scans.ToArray()
            : scans.Where(scan => SavedScanMatchesFilter(scan, normalized)).ToArray();
    }
}
