namespace Zenmap.Windows.Services;

public static class WindowsPaths
{
    public static string ConfigRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "zenmap-native");

    public static string SettingsPath => Path.Combine(ConfigRoot, "settings.json");
    public static string SavedScansIndexPath => Path.Combine(ConfigRoot, "saved-scans.json");
    public static string CustomProfilesPath => Path.Combine(ConfigRoot, "custom-profiles.json");
    public static string SavedScansDirectory => Path.Combine(ConfigRoot, "saved-scans");

    public static void EnsureConfigDirectories()
    {
        Directory.CreateDirectory(ConfigRoot);
        Directory.CreateDirectory(SavedScansDirectory);
    }
}
