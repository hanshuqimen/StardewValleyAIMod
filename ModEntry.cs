using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValleyAIMod.Menus;
using StardewValleyAIMod.Services;

namespace StardewValleyAIMod;

/// <summary>
/// mod 入口。负责加载配置、初始化 AI 中转服务、监听快捷键、
/// 在玩家站在 NPC 附近时打开 AI 对话菜单。
///
/// 架构说明：
/// - mod 本身不做任何模型推理，所有 AI 能力都由玩家在 config 中填写的
///   ApiUrl / ApiKey / Model 提供，<see cref="AiService"/> 只负责把请求转发出去。
/// - 在每轮对话（或首次对话前的预热请求）中，会把 <see cref="Data.CharacterPrompts"/>
///   里对应 NPC 的人设作为 system 消息发给目标接口，用于塑造角色、避免出戏。
/// </summary>
internal class ModEntry : Mod
{
    private ModConfig _config = null!;
    private AiService _ai = null!;
    private ConversationStore _store = null!;

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        _ai = new AiService(_config, Monitor);
        _store = new ConversationStore(_config);

        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (_config.IsValid)
            Monitor.Log($"AI 中转已就绪，目标接口：{_config.ApiUrl}，模型：{_config.Model}", LogLevel.Info);
        else
            Monitor.Log("未配置 ApiUrl，请在 Mods/StardewValleyAIMod/config.json 中填写后再使用。", LogLevel.Warn);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (!_config.IsValid)
            Game1.addHUDMessage(new HUDMessage("AI 对话 mod 未配置 ApiUrl，请先编辑 config.json", HUDMessage.error_type));
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!_config.ToggleKey.JustPressed())
            return;

        // 已有菜单打开时不抢
        if (Game1.activeClickableMenu != null) return;
        if (Game1.currentLocation == null || Game1.player == null) return;
        if (!Context.IsPlayerFree) return;

        var npc = FindNearestNpc();
        if (npc == null)
        {
            Game1.addHUDMessage(new HUDMessage("附近没有可以对话的 NPC", HUDMessage.error_type));
            return;
        }

        if (!_config.IsValid)
        {
            Game1.addHUDMessage(new HUDMessage("AI 对话 mod 未配置 ApiUrl", HUDMessage.error_type));
            return;
        }

        Game1.activeClickableMenu = new AiDialogueMenu(npc, _ai, _store, _config, Monitor);
        Game1.playSound("bigSelect");
    }

    /// <summary>
    /// 在玩家当前所在地点、配置范围内找最近的可对话 NPC。
    /// </summary>
    private NPC? FindNearestNpc()
    {
        var loc = Game1.currentLocation;
        if (loc?.characters == null || loc.characters.Count == 0) return null;

        var playerTile = Game1.player.getTileLocation();
        NPC? best = null;
        float bestDist = float.MaxValue;
        var range = _config.InteractionRange;

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
