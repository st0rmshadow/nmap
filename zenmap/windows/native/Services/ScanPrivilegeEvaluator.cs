using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public static class ScanPrivilegeEvaluator
{
    private static readonly HashSet<string> AdministratorRequiredFlags = new(StringComparer.Ordinal)
    {
        "-sS",
        "-sU",
        "-O",
        "-A",
        "--traceroute",
        "-sA",
        "-sW",
        "-sM",
        "-sN",
        "-sF",
        "-sX",
        "-sY",
        "-sZ",
    };

    public static ZenmapScanExecutionModeDetail Evaluate(IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (AdministratorRequiredFlags.Contains(argument))
            {
                return new ZenmapScanExecutionModeDetail
                {
                    Mode = ZenmapScanExecutionMode.Administrator,
                    Reason = $"{argument} requires administrator privileges.",
                };
            }
        }

        return new ZenmapScanExecutionModeDetail
        {
            Mode = ZenmapScanExecutionMode.NormalUser,
        };
    }
}
