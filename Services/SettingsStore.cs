using System;
using System.IO;
using System.Text.Json;
using StardewModdingAPI;

namespace StardewValleyAIMod.Services;

internal class SettingsStore
{
    private readonly string _path;
    private readonly IMonitor _monitor;

    public SettingsStore(string modDirectory, IMonitor monitor)
    {
        _path = Path.Combine(modDirectory, "settings.json");
        _monitor = monitor;
    }

    public ModSettings Load()
    {
        var s = new ModSettings();
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<ModSettings>(json);
                if (loaded != null)
                {
                    s.ApiUrl = loaded.ApiUrl ?? "";
                    s.ApiKey = loaded.ApiKey ?? "";
                    s.Model = string.IsNullOrWhiteSpace(loaded.Model) ? s.Model : loaded.Model;
                    s.Temperature = loaded.Temperature;
                    s.MaxTokens = loaded.MaxTokens;
                    s.RequestTimeoutSeconds = loaded.RequestTimeoutSeconds;
                    s.ConversationHistoryLength = loaded.ConversationHistoryLength;
                    s.SendPrimingRequest = loaded.SendPrimingRequest;
                    s.ExtraSystemInstruction = loaded.ExtraSystemInstruction ?? s.ExtraSystemInstruction;
                    s.InteractionRange = loaded.InteractionRange;
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"读取 settings.json 失败，使用默认设置：{ex.Message}", LogLevel.Warn);
        }
        return s;
    }

    public void Save(ModSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
            _monitor.Log("已保存 AI 设置到 settings.json", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _monitor.Log($"保存 settings.json 失败：{ex.Message}", LogLevel.Error);
        }
    }
}
