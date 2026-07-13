using System.Text.Json;
using System.Text.Json.Serialization;
using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public static class JsonSerialization
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static string EncodeProfiles(IEnumerable<ScanProfile> profiles) =>
        JsonSerializer.Serialize(profiles, Options);

    public static List<ScanProfile> DecodeProfiles(string payload) =>
        JsonSerializer.Deserialize<List<ScanProfile>>(payload, Options) ?? [];

    public static string EncodeSavedScans(IEnumerable<SavedScan> scans) =>
        JsonSerializer.Serialize(scans.Where(scan => !scan.Ephemeral), Options);

    public static List<SavedScan> DecodeSavedScans(string payload) =>
        JsonSerializer.Deserialize<List<SavedScan>>(payload, Options) ?? [];

    public static void WriteJsonFile(string path, string payload)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, payload);
    }

    public static string ReadJsonFile(string path, string defaultValue) =>
        File.Exists(path) ? File.ReadAllText(path) : defaultValue;
}
