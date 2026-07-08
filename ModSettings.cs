namespace StardewValleyAIMod;

/// <summary>
/// 运行时的 AI 设置。这些字段不再要求玩家改代码或改 config.json，
/// 而是由游戏内的「设置窗口」(<see cref="Menus.SettingsMenu"/>) 输入后保存到
/// mod 目录下的 settings.json。mod 启动时读入，请求时直接转发给玩家填写的接口。
/// </summary>
internal class ModSettings
{
    /// <summary>
    /// 玩家自备的 AI 接口地址。需为 OpenAI 兼容的 chat/completions 端点。
    /// 例：https://api.openai.com/v1/chat/completions
    /// </summary>
    public string ApiUrl { get; set; } = "";

    /// <summary>
    /// 玩家自备的 API Key。mod 仅以 Authorization: Bearer 形式转发，不做任何推理。
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>请求体里的 model 字段，玩家按自己接入的服务填写。</summary>
    public string Model { get; set; } = "gpt-3.5-turbo";

    /// <summary>采样温度。</summary>
    public float Temperature { get; set; } = 0.8f;

    /// <summary>单条回复最大 token 数。</summary>
    public int MaxTokens { get; set; } = 220;

    /// <summary>请求超时（秒）。</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>保留的对话历史轮数（用于多轮上下文）。</summary>
    public int ConversationHistoryLength { get; set; } = 6;

    /// <summary>
    /// 是否在首次对话前向目标接口单独发送一次"人设塑造"请求。
    /// 默认 false：人设作为 system 消息随每条请求一起发送（兼容性更好）。
    /// </summary>
    public bool SendPrimingRequest { get; set; } = false;

    /// <summary>额外追加到 system 消息末尾的通用指令，例如限定回复语言。</summary>
    public string ExtraSystemInstruction { get; set; } =
        "请用简短、口语化的中文回复，长度控制在两三句话以内。";

    /// <summary>与 NPC 互动的最大距离（格）。</summary>
    public float InteractionRange { get; set; } = 2.5f;

    /// <summary>配置是否可用（至少填了网址）。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(ApiUrl);

    /// <summary>供设置窗口编辑用的浅拷贝；保存时再写回共享实例。</summary>
    public ModSettings Clone() => (ModSettings)MemberwiseClone();
}
