namespace Zenmap.Windows.Services;

public static class NmapPathResolver
{
    public static string? ResolveNmapBinary(string? preferred = null)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            candidates.Add(preferred);
        }

        candidates.AddRange(
        [
            Path.Combine(AppContext.BaseDirectory, "nmap.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "nmap.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "nmap.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nmap", "nmap.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Nmap", "nmap.exe"),
        ]);

        var pathFromEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathFromEnv))
        {
            foreach (var directory in pathFromEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                candidates.Add(Path.Combine(directory.Trim(), "nmap.exe"));
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    public static string ResolveNmapDataDirectory(string nmapBinary)
    {
        var binaryPath = new FileInfo(nmapBinary);
        var binaryDir = binaryPath.Directory?.FullName ?? AppContext.BaseDirectory;

        // Windows official installs keep nmap-services / scripts beside nmap.exe.
        // Unix layouts use ../share/nmap. Prefer a directory that actually contains
        // nmap data files so setting NMAPDIR does not break elevated scans.
        var candidates = new[]
        {
            binaryDir,
            Path.Combine(binaryPath.Directory?.Parent?.FullName ?? "", "share", "nmap"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "nmap"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nmap"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Nmap"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nmap", "share", "nmap"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Nmap", "share", "nmap"),
        };

        foreach (var candidate in candidates)
        {
            if (IsNmapDataDirectory(candidate))
            {
                return candidate;
            }
        }

        return binaryDir;
    }

    private static bool IsNmapDataDirectory(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        Directory.Exists(path) &&
        (File.Exists(Path.Combine(path, "nmap-services")) ||
         File.Exists(Path.Combine(path, "nse_main.lua")));
}
