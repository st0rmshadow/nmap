namespace Zenmap.Windows.Models;

public sealed class ScanProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required string Arguments { get; init; }
    public required string Description { get; init; }
    public bool IsBuiltIn { get; init; } = true;
}

public sealed class ScannedPort
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string HostAddress { get; init; }
    public required string ProtocolName { get; init; }
    public required string PortNumber { get; init; }
    public required string State { get; init; }
    public string ServiceName { get; init; } = "";
    public string Product { get; init; } = "";
    public string Version { get; init; } = "";
    public string ExtraInfo { get; init; } = "";

    public string ServiceSummary =>
        string.Join(' ', new[] { Product, Version, ExtraInfo }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

public sealed class ScannedHost
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Address { get; init; }
    public string Hostname { get; init; } = "";
    public string Status { get; init; } = "unknown";
    public IReadOnlyList<ScannedPort> Ports { get; init; } = Array.Empty<ScannedPort>();

    public string DisplayName => string.IsNullOrWhiteSpace(Hostname) ? Address : Hostname;

    public int OpenPortCount => Ports.Count(port => port.State == "open");
}

public sealed class SavedScan
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; init; }
    public required string Command { get; init; }
    public required string XmlPath { get; init; }
    public required DateTimeOffset ScannedAt { get; init; }
    public int HostCount { get; init; }
    public int PortCount { get; init; }
    public string Notes { get; init; } = "";
    public string Tags { get; init; } = "";
}
