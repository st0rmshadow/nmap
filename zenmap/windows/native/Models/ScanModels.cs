namespace Zenmap.Windows.Models;

public sealed class ScanProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsBuiltIn { get; set; } = true;

    public string KindLabel => IsBuiltIn ? "built-in" : "custom";
}

public sealed class ScannedPort
{
    public Guid Id { get; } = Guid.NewGuid();
    public string HostAddress { get; set; } = "";
    public string ProtocolName { get; set; } = "";
    public string PortNumber { get; set; } = "";
    public string State { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string Product { get; set; } = "";
    public string Version { get; set; } = "";
    public string ExtraInfo { get; set; } = "";

    public string ServiceSummary =>
        string.Join(' ', new[] { Product, Version, ExtraInfo }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

public sealed class ScannedHost
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Address { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string Status { get; set; } = "unknown";
    public List<ScannedPort> Ports { get; set; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(Hostname) ? Address : Hostname;

    public int OpenPortCount => Ports.Count(port => port.State == "open");
}

public sealed class SavedScan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Command { get; set; } = "";
    public string XmlPath { get; set; } = "";
    public DateTimeOffset ScannedAt { get; set; }
    public int HostCount { get; set; }
    public int PortCount { get; set; }
    public string Notes { get; set; } = "";
    public string Tags { get; set; } = "";
    public bool Ephemeral { get; set; }

    public string ScannedAtLocal => ScannedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string DisplayTitle => Ephemeral ? $"{Title} (session only)" : Title;
}
