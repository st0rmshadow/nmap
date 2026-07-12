using System.Xml.Linq;
using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public static class XmlParsing
{
    public static IReadOnlyList<ScannedHost> ParseNmapXml(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<ScannedHost>();
        }

        try
        {
            var document = XDocument.Load(path);
            return document.Root?
                .Elements("host")
                .Select(ParseHost)
                .Where(host => host is not null)
                .Cast<ScannedHost>()
                .ToArray() ?? Array.Empty<ScannedHost>();
        }
        catch (Exception)
        {
            return Array.Empty<ScannedHost>();
        }
    }

    private static ScannedHost? ParseHost(XElement hostElement)
    {
        var status = hostElement.Element("status")?.Attribute("state")?.Value ?? "unknown";

        var address = hostElement.Elements("address")
            .Select(element => element.Attribute("addr")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

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
            Ports = ports,
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
