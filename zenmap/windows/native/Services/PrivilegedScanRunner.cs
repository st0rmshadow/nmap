using System.Diagnostics;
using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

/// <summary>
/// Runs privileged scans through a UAC-elevated helper process.
/// </summary>
public static class PrivilegedScanRunner
{
    public static ProcessStartInfo CreateElevatedStartInfo(
        string nmapExecutable,
        IReadOnlyList<string> arguments,
        string? xmlOutputPath)
    {
        var argumentList = new List<string>(arguments);
        if (!string.IsNullOrWhiteSpace(xmlOutputPath))
        {
            argumentList.Add("-oX");
            argumentList.Add(xmlOutputPath);
        }

        return new ProcessStartInfo
        {
            FileName = nmapExecutable,
            Arguments = string.Join(' ', argumentList.Select(QuoteArgument)),
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = false,
        };
    }

    public static bool RequiresElevation(ZenmapScanExecutionModeDetail requirement) =>
        requirement.Mode == ZenmapScanExecutionMode.Administrator;

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ') ? $"\"{argument}\"" : argument;
}
