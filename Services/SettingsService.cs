using System.IO;
using System.Text.Json;
using MC_Helper.Helpers;
using MC_Helper.Models;

namespace MC_Helper.Services;

public class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        AppContext.BaseDirectory, "MC_Helper_config");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public RootSettings Settings { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<RootSettings>(json, JsonOptions) ?? new RootSettings();

                Settings.ModeSwitching ??= new ModeSettings();
                Settings.Click ??= new ClickToolSettings();
                Settings.Fishing ??= new FishingSettings();
            }
            else
            {
                Settings = new RootSettings();
                Save();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("加载设置失败", ex);
            Settings = new RootSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
