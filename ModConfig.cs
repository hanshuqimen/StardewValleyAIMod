using StardewModdingAPI.Utilities;
using SButton = StardewModdingAPI.SButton;

namespace StardewValleyAIMod;

/// <summary>
/// 仅保留快捷键等"行为类"开关，且全部带默认值，玩家无需手动编辑 config.json。
/// 玩家自备的 AI 网址 / Key / 模型等敏感字段不在这里，而是通过游戏内的设置窗口
/// (<see cref="Menus.SettingsMenu"/>) 输入后存到 settings.json（见 <see cref="ModSettings"/>）。
/// </summary>
internal class ModConfig
{
    /// <summary>站在 NPC 附近时打开 AI 对话的快捷键。</summary>
    public KeybindList ToggleKey { get; set; } = new(SButton.L);

    /// <summary>打开 AI 设置窗口的快捷键。</summary>
    public KeybindList SettingsKey { get; set; } = new(SButton.K);
}
