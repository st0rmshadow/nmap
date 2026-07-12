using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public static class BuiltInProfiles
{
    public static IReadOnlyList<ScanProfile> All { get; } =
    [
        new() { Name = "Quick Scan", Arguments = "-T4 -F", Description = "Fast scan of common ports." },
        new()
        {
            Name = "TCP Connect",
            Arguments = "-sT -sV -T4 -v",
            Description = "Uses TCP connect scanning when raw-packet SYN scans are blocked or unreliable.",
        },
        new() { Name = "Regular Scan", Arguments = "", Description = "Default Nmap TCP scan." },
        new() { Name = "Service Detection", Arguments = "-sV", Description = "Detect service and version information." },
        new()
        {
            Name = "Aggressive Scan",
            Arguments = "-A",
            Description = "Enable OS detection, version detection, scripts, and traceroute.",
        },
        new() { Name = "Ping Scan", Arguments = "-sn", Description = "Discover live hosts without port scanning." },
        new() { Name = "List Scan", Arguments = "-sL", Description = "List targets without sending packets." },
        new() { Name = "Intense Scan", Arguments = "-T4 -A -v", Description = "More detailed scan with verbose output." },
        new()
        {
            Name = "Intense Scan + UDP",
            Arguments = "-sS -sU -T4 -A -v",
            Description = "Detailed TCP and UDP scan. May require administrator privileges.",
        },
        new()
        {
            Name = "Slow Comprehensive Scan",
            Arguments = "-sS -sU -T4 -A -v -PE -PP -PS80,443 -PA3389 -PU40125 -PY -g 53 --script default,safe",
            Description = "Broad scan inspired by classic Zenmap profiles.",
        },
        new() { Name = "Custom", Arguments = "-sV", Description = "Edit arguments manually.", IsBuiltIn = false },
    ];
}
