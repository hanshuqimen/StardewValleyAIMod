using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValleyAIMod.Menus;
using StardewValleyAIMod.Services;

namespace StardewValleyAIMod;

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
            Game1.addHUDMessage(new HUDMessage("按 K 设置 AI 接口（网址 + API Key），再走到 NPC 旁按 L 对话"));
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (Game1.activeClickableMenu != null) return;

        if (_config.SettingsKey.JustPressed())
        {
            OpenSettings();
            return;
        }

        if (!_config.ToggleKey.JustPressed()) return;
        if (Game1.currentLocation == null || Game1.player == null) return;
        if (!Context.IsPlayerFree) return;

        if (!_settings.IsValid)
        {
            Game1.addHUDMessage(new HUDMessage("尚未配置 AI 接口，已为你打开设置窗口"));
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

    private NPC? FindNearestNpc()
    {
        var loc = Game1.currentLocation;
        if (loc == null || loc.characters.Count == 0) return null;

        var playerTile = Game1.player.Position / Game1.tileSize;
        NPC? best = null;
        float bestDist = float.MaxValue;
        var range = _settings.InteractionRange;

        foreach (var npc in loc.characters)
        {
            if (npc == null || !npc.IsVillager || npc.IsInvisible) continue;
            var npcTile = npc.Position / Game1.tileSize;
            var d = Vector2.Distance(playerTile, npcTile);
            if (d <= range && d < bestDist)
            {
                bestDist = d;
                best = npc;
            }
        }
        return best;
    }
}
