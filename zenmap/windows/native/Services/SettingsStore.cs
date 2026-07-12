using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public sealed class SettingsStore
{
    public AppSettings Settings { get; set; }

    public SettingsStore()
    {
        Settings = Load();
    }

    public void Save()
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(Settings, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        });
        JsonSerialization.WriteJsonFile(WindowsPaths.SettingsPath, payload);
    }

    private static AppSettings Load()
    {
        var raw = JsonSerialization.ReadJsonFile(WindowsPaths.SettingsPath, "{}");
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(raw, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
            }) ?? new AppSettings();
        }
        catch (System.Text.Json.JsonException)
        {
            return new AppSettings();
        }
    }
}
