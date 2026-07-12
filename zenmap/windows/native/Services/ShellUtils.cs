using System.Text;

namespace Zenmap.Windows.Services;

public static class ShellUtils
{
    public static IReadOnlyList<string> ShellSplit(string commandLine)
    {
        commandLine = commandLine.Trim();
        if (string.IsNullOrEmpty(commandLine))
        {
            return Array.Empty<string>();
        }

        var parts = new List<string>();
        var current = new StringBuilder();
        var inSingle = false;
        var inDouble = false;

        foreach (var character in commandLine)
        {
            if (character == '\'' && !inDouble)
            {
                inSingle = !inSingle;
                continue;
            }

            if (character == '"' && !inSingle)
            {
                inDouble = !inDouble;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inSingle && !inDouble)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }

    public static IReadOnlyList<string> SplitTargets(string targetText) =>
        targetText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToArray();

    public static string ShellEscape(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
