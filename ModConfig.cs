using StardewModdingAPI.Utilities;
using SButton = StardewModdingAPI.SButton;

namespace StardewValleyAIMod;

internal class ModConfig
{
    public KeybindList ToggleKey { get; set; } = new(SButton.L);

    public KeybindList SettingsKey { get; set; } = new(SButton.K);
}
