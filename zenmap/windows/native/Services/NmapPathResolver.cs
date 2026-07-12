namespace Zenmap.Windows.Services;

public static class NmapPathResolver
{
    public static string ResolveNmapExecutable()
    {
        var appDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDirectory, "nmap.exe"),
            Path.Combine(appDirectory, "..", "nmap.exe"),
            Path.Combine(appDirectory, "..", "..", "nmap.exe"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return "nmap.exe";
    }
}
