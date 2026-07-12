namespace Zenmap.Windows.Models;

public sealed class AppSettings
{
    public bool AutoAddVerbose { get; set; }
    public bool AutoAddStatsEvery { get; set; } = true;
    public string StatsEveryValue { get; set; } = "1s";
    public string DefaultTarget { get; set; } = "scanme.nmap.org";
    public string DefaultProfileName { get; set; } = "Quick Scan";
    public string NmapBinary { get; set; } = "nmap";
}
