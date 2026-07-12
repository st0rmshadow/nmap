using System.Text.RegularExpressions;
using System.Xml.Linq;
using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public static class XmlParsing
{
    private static readonly Regex CompleteHostRegex = new(
        @"<host\b[^>]*>.*?</host>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CompleteHostHintRegex = new(
        @"<hosthint\b[^>]*>.*?</hosthint>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DiscoveredOpenPortRegex = new(
        @"Discovered open port (\d+)/(tcp|udp|sctp) on (\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ScanReportRegex = new(
        @"Nmap scan report for (?:(\S+) \((\d[^)]*)\)|(\S+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<ScannedHost> ParseNmapXml(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<ScannedHost>();
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception)
        {
            return Array.Empty<ScannedHost>();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<ScannedHost>();
        }

        try
        {
            var document = XDocument.Parse(text, LoadOptions.None);
            var finished = document.Root?
                .Elements("host")
                .Select(ParseHost)
                .Where(host => host is not null)
                .Cast<ScannedHost>()
                .ToArray() ?? Array.Empty<ScannedHost>();
            var hints = document.Root?
                .Elements("hosthint")
                .Select(ParseHost)
                .Where(host => host is not null)
                .Cast<ScannedHost>()
                .ToArray() ?? Array.Empty<ScannedHost>();
            return MergeScanHosts(finished, hints);
        }
        catch (Exception)
        {
            return MergeScanHosts(
                ParseCompleteElements(text, CompleteHostRegex),
                ParseCompleteElements(text, CompleteHostHintRegex));
        }
    }

    public static IReadOnlyList<ScannedHost> ParseLiveOutputHosts(string outputText)
    {
        var hostsByAddress = new Dictionary<string, ScannedHost>(StringComparer.Ordinal);

        foreach (Match match in ScanReportRegex.Matches(outputText))
        {
            var hostname = match.Groups[1].Success ? match.Groups[1].Value : "";
            var parentheticalIp = match.Groups[2].Success ? match.Groups[2].Value : "";
            var bareTarget = match.Groups[3].Success ? match.Groups[3].Value : "";
            var address = string.IsNullOrWhiteSpace(parentheticalIp) ? bareTarget : parentheticalIp;
            if (string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            if (hostsByAddress.TryGetValue(address, out var existing))
            {
                if (string.IsNullOrWhiteSpace(existing.Hostname) && !string.IsNullOrWhiteSpace(hostname))
                {
                    existing.Hostname = hostname;
                }
            }
            else
            {
                hostsByAddress[address] = new ScannedHost
                {
                    Address = address,
                    Hostname = hostname,
                    Status = "up",
                };
            }
        }

        foreach (Match match in DiscoveredOpenPortRegex.Matches(outputText))
        {
            var portNumber = match.Groups[1].Value;
            var protocolName = match.Groups[2].Value.ToLowerInvariant();
            var address = match.Groups[3].Value;
            if (!hostsByAddress.TryGetValue(address, out var host))
            {
                host = new ScannedHost { Address = address, Status = "up" };
                hostsByAddress[address] = host;
            }

            if (host.Ports.Any(port =>
                    string.Equals(port.ProtocolName, protocolName, StringComparison.OrdinalIgnoreCase) &&
                    port.PortNumber == portNumber))
            {
                continue;
            }

            host.Ports.Add(new ScannedPort
            {
                HostAddress = address,
                ProtocolName = protocolName,
                PortNumber = portNumber,
                State = "open",
            });
        }

        return hostsByAddress.Values.ToArray();
    }

    public static IReadOnlyList<ScannedHost> MergeScanHosts(
        IReadOnlyList<ScannedHost> primary,
        IReadOnlyList<ScannedHost> secondary)
    {
        var merged = new Dictionary<string, ScannedHost>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var source in new[] { primary, secondary })
        {
            foreach (var host in source)
            {
                var key = string.IsNullOrWhiteSpace(host.Address) ? host.Hostname : host.Address;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!merged.TryGetValue(key, out var existing))
                {
                    merged[key] = CloneHost(host);
                    order.Add(key);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(existing.Hostname) && !string.IsNullOrWhiteSpace(host.Hostname))
                {
                    existing.Hostname = host.Hostname;
                }

                if ((string.IsNullOrWhiteSpace(existing.Status) || existing.Status == "unknown") &&
                    !string.IsNullOrWhiteSpace(host.Status))
                {
                    existing.Status = host.Status;
                }

                existing.Ports = MergePorts(existing.Ports, host.Ports);
            }
        }

        return order.Select(key => merged[key]).ToArray();
    }

    public static string HostsFingerprint(IReadOnlyList<ScannedHost> hosts) =>
        string.Join(
            '\n',
            hosts.Select(host =>
                $"{host.Address}|{host.Hostname}|{host.Status}|" +
                string.Join(
                    ';',
                    host.Ports.Select(port =>
                        $"{port.ProtocolName}/{port.PortNumber}/{port.State}/{port.ServiceName}/{port.Product}/{port.Version}"))));

    private static IReadOnlyList<ScannedHost> ParseCompleteElements(string text, Regex pattern)
    {
        var hosts = new List<ScannedHost>();
        foreach (Match match in pattern.Matches(text))
        {
            try
            {
                var host = ParseHost(XElement.Parse(match.Value));
                if (host is not null)
                {
                    hosts.Add(host);
                }
            }
            catch (Exception)
            {
            }
        }

        return hosts;
    }

    private static List<ScannedPort> MergePorts(IEnumerable<ScannedPort> primary, IEnumerable<ScannedPort> secondary)
    {
        var merged = new Dictionary<string, ScannedPort>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        foreach (var source in new[] { primary, secondary })
        {
            foreach (var port in source)
            {
                var key = $"{port.ProtocolName}/{port.PortNumber}";
                if (!merged.TryGetValue(key, out var existing))
                {
                    merged[key] = ClonePort(port);
                    order.Add(key);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(existing.ServiceName)) existing.ServiceName = port.ServiceName;
                if (string.IsNullOrWhiteSpace(existing.Product)) existing.Product = port.Product;
                if (string.IsNullOrWhiteSpace(existing.Version)) existing.Version = port.Version;
                if (string.IsNullOrWhiteSpace(existing.ExtraInfo)) existing.ExtraInfo = port.ExtraInfo;
                if (string.IsNullOrWhiteSpace(existing.State) || existing.State == "unknown") existing.State = port.State;
            }
        }

        return order.Select(key => merged[key]).ToList();
    }

    private static ScannedHost CloneHost(ScannedHost host) => new()
    {
        Address = host.Address,
        Hostname = host.Hostname,
        Status = host.Status,
        Ports = host.Ports.Select(ClonePort).ToList(),
    };

    private static ScannedPort ClonePort(ScannedPort port) => new()
    {
        HostAddress = port.HostAddress,
        ProtocolName = port.ProtocolName,
        PortNumber = port.PortNumber,
        State = port.State,
        ServiceName = port.ServiceName,
        Product = port.Product,
        Version = port.Version,
        ExtraInfo = port.ExtraInfo,
    };

    private static ScannedHost? ParseHost(XElement hostElement)
    {
        var status = hostElement.Element("status")?.Attribute("state")?.Value ?? "unknown";

        var address = "";
        foreach (var addressElement in hostElement.Elements("address"))
        {
            var addrType = addressElement.Attribute("addrtype")?.Value ?? "";
            var candidate = addressElement.Attribute("addr")?.Value ?? "";
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(address) || addrType is "ipv4" or "ipv6")
            {
                address = candidate;
                if (addrType is "ipv4" or "ipv6")
                {
                    break;
                }
            }
        }

        var hostname = hostElement.Element("hostnames")?
            .Element("hostname")?
            .Attribute("name")?
            .Value ?? "";

        var ports = hostElement.Element("ports")?
            .Elements("port")
            .Select(port => ParsePort(port, address))
            .Where(port => port is not null)
            .Cast<ScannedPort>()
            .ToArray() ?? Array.Empty<ScannedPort>();

        if (string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(hostname))
        {
            return null;
        }

        return new ScannedHost
        {
            Address = address,
            Hostname = hostname,
            Status = status,
            Ports = ports.ToList(),
        };
    }

    private static ScannedPort? ParsePort(XElement portElement, string hostAddress)
    {
        var state = portElement.Element("state")?.Attribute("state")?.Value ?? "unknown";
        var service = portElement.Element("service");

        return new ScannedPort
        {
            HostAddress = hostAddress,
            ProtocolName = portElement.Attribute("protocol")?.Value ?? "",
            PortNumber = portElement.Attribute("portid")?.Value ?? "",
            State = state,
            ServiceName = service?.Attribute("name")?.Value ?? "",
            Product = service?.Attribute("product")?.Value ?? "",
            Version = service?.Attribute("version")?.Value ?? "",
            ExtraInfo = service?.Attribute("extrainfo")?.Value ?? "",
        };
    }
}
