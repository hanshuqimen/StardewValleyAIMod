using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValleyAIMod.Menus;
using StardewValleyAIMod.Services;

namespace StardewValleyAIMod;

/// <summary>
/// mod 入口。负责加载快捷键与 AI 设置、初始化中转服务、监听快捷键：
/// - 按 K 打开「AI 设置窗口」，玩家在其中输入网址/API/模型，无需改代码、无需配置环境；
/// - 按 L 站在 NPC 附近打开「AI 对话窗口」，mod 把消息转发给玩家填写的接口。
///
/// 架构说明：
/// - mod 不做任何模型推理，所有 AI 能力都来自玩家在设置窗口填写的
///   ApiUrl / ApiKey / Model，<see cref="AiService"/> 只负责把请求转发出去。
/// - 每轮对话（或首次对话前的预热请求）会把 <see cref="Data.CharacterPrompts"/>
///   里对应 NPC 的人设作为 system 消息发给目标接口，用于塑造角色、避免出戏。
/// </summary>
internal class ModEntry : Mod
{
    private ModConfig _config = null!;
    private ModSettings _settings = null!;
    private SettingsStore _settingsStore = null!;
    private AiService _ai = null!;
    private ConversationStore _store = null!;

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        _settingsStore = new SettingsStore(helper.DirectoryPath, Monitor);
        _settings = _settingsStore.Load();
        _ai = new AiService(_settings, Monitor);
        _store = new ConversationStore(_settings);

        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        Monitor.Log("AI 对话 mod 已加载。按 K 打开设置窗口填写你的 AI 网址和 Key；填好后走到 NPC 旁按 L 对话。", LogLevel.Info);
        if (_settings.IsValid)
            Monitor.Log($"已读取设置，目标接口：{_settings.ApiUrl}，模型：{_settings.Model}", LogLevel.Info);
        else
            Monitor.Log("尚未配置 AI 网址。请进游戏后按 K 打开设置窗口。", LogLevel.Warn);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (!_settings.IsValid)
            Game1.addHUDMessage(new HUDMessage("按 K 设置 AI 接口（网址 + API Key），再走到 NPC 旁按 L 对话", HUDMessage.info_type) { noIcon = true });
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        // 已有菜单打开时不抢
        if (Game1.activeClickableMenu != null) return;

        // 设置键：任何可操作状态下都能打开（即便没存档也允许，方便玩家先填好）
        if (_config.SettingsKey.JustPressed())
        {
            OpenSettings();
            return;
        }

        if (!_config.ToggleKey.JustPressed()) return;
        if (Game1.currentLocation == null || Game1.player == null) return;
        if (!Context.IsPlayerFree) return;

        // 没配置就直接引导去设置窗口
        if (!_settings.IsValid)
        {
            Game1.addHUDMessage(new HUDMessage("尚未配置 AI 接口，已为你打开设置窗口", HUDMessage.info_type) { noIcon = true });
            OpenSettings();
            return;
        }

        var npc = FindNearestNpc();
        if (npc == null)
        {
            Game1.addHUDMessage(new HUDMessage("附近没有可以对话的 NPC", HUDMessage.error_type));
            return;
        }

        Game1.activeClickableMenu = new AiDialogueMenu(npc, _ai, _store, _settings, Monitor);
        Game1.playSound("bigSelect");
    }

    private void OpenSettings()
    {
        Game1.activeClickableMenu = new SettingsMenu(_settings, _settingsStore, _ai, Monitor);
        Game1.playSound("bigSelect");
    }

    /// <summary>在玩家当前所在地点、配置范围内找最近的可对话 NPC。</summary>
    private NPC? FindNearestNpc()
    {
        var loc = Game1.currentLocation;
        if (loc?.characters == null || loc.characters.Count == 0) return null;

        var playerTile = Game1.player.getTileLocation();
        NPC? best = null;
        float bestDist = float.MaxValue;
        var range = _settings.InteractionRange;

        foreach (var npc in loc.characters)
        {
            if (npc == null || !npc.isVillager() || npc.IsInvisible) continue;
            var d = Vector2.Distance(playerTile, npc.getTileLocation());
            if (d <= range && d < bestDist)
            {
                bestDist = d;
                best = npc;
            }
        }
        return best;
    }
}
