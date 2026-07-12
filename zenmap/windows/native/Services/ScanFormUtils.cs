namespace Zenmap.Windows.Services;

public static class ScanFormUtils
{
    private static readonly HashSet<string> SkipNextOutputFlags = new(StringComparer.Ordinal)
    {
        "-oX", "-oA", "-oN", "-oG", "-oS",
    };

    public static (string Arguments, string Targets) ValuesFromCommand(string command)
    {
        var parts = ShellUtils.ShellSplit(command).ToList();
        if (parts.Count > 0)
        {
            var first = parts[0];
            var firstName = first.Contains('/') ? first[(first.LastIndexOf('/') + 1)..] : first;
            if (first == "nmap" || firstName == "nmap" || firstName == "nmap.exe")
            {
                parts.RemoveAt(0);
            }
        }

        var argumentValues = new List<string>();
        var targetValues = new List<string>();
        var index = 0;

        while (index < parts.Count)
        {
            var part = parts[index];
            if (SkipNextOutputFlags.Contains(part))
            {
                index += 2;
                continue;
            }

            if (part.StartsWith("-oX", StringComparison.Ordinal) ||
                part.StartsWith("-oA", StringComparison.Ordinal) ||
                part.StartsWith("-oN", StringComparison.Ordinal) ||
                part.StartsWith("-oG", StringComparison.Ordinal) ||
                part.StartsWith("-oS", StringComparison.Ordinal))
            {
                index += 1;
                continue;
            }

            if (part is "--stylesheet" or "--webxml" or "--resume" or "-iL" or "-iR")
            {
                argumentValues.Add(part);
                if (index + 1 < parts.Count)
                {
                    argumentValues.Add(parts[index + 1]);
                    index += 2;
                }
                else
                {
                    index += 1;
                }

                continue;
            }

            if (part.StartsWith('-'))
            {
                argumentValues.Add(part);
            }
            else
            {
                targetValues.Add(part);
            }

            index += 1;
        }

        return (string.Join(' ', argumentValues), string.Join(' ', targetValues));
    }
}
