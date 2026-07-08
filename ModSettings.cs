namespace StardewValleyAIMod;

internal class ModSettings
{
    public string ApiUrl { get; set; } = "";

    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "gpt-3.5-turbo";

    public float Temperature { get; set; } = 0.8f;

    public int MaxTokens { get; set; } = 220;

    public int RequestTimeoutSeconds { get; set; } = 30;

    public int ConversationHistoryLength { get; set; } = 6;

    public bool SendPrimingRequest { get; set; } = false;

    public string ExtraSystemInstruction { get; set; } =
        "请用简短、口语化的中文回复，长度控制在两三句话以内。";

    public float InteractionRange { get; set; } = 2.5f;

    public bool IsValid => !string.IsNullOrWhiteSpace(ApiUrl);

    public ModSettings Clone() => (ModSettings)MemberwiseClone();
}
