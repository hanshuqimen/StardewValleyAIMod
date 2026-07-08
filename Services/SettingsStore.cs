using System;
using System.IO;
using System.Text.Json;
using StardewModdingAPI;

namespace StardewValleyAIMod.Services;

/// <summary>
/// 负责把玩家在设置窗口里填写的 <see cref="ModSettings"/> 读写到 mod 目录下的
/// settings.json。这样玩家"安装 mod 即可用"——无需改代码、无需配置环境，
/// 进游戏按设置键填好网址和 Key 即可。
/// </summary>
internal class SettingsStore
{
    private readonly string _path;
    private readonly IMonitor _monitor;

    public SettingsStore(string modDirectory, IMonitor monitor)
    {
        _path = Path.Combine(modDirectory, "settings.json");
        _monitor = monitor;
    }

    /// <summary>读取设置；文件不存在或损坏时返回带默认值的实例。</summary>
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

    /// <summary>保存设置到 settings.json。</summary>
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
