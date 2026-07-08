using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using SButton = StardewModdingAPI.SButton;

namespace StardewValleyAIMod;

/// <summary>
/// 玩家可配置项。所有 API 相关字段都由玩家自行填写，mod 不内置任何密钥，
/// 仅在运行时把这些字段拼装成请求转发给玩家指定的服务端。
/// </summary>
internal class ModConfig
{
    /// <summary>
    /// 玩家自备的 AI 接口地址。需为 OpenAI 兼容的 chat/completions 端点。
    /// 例：https://api.openai.com/v1/chat/completions
    /// </summary>
    public string ApiUrl { get; set; } = "";

    /// <summary>
    /// 玩家自备的 API Key。mod 仅以 Authorization: Bearer 形式转发，不做任何本地保存处理。
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// 请求体里的 model 字段，玩家按自己接入的服务填写。
    /// </summary>
    public string Model { get; set; } = "gpt-3.5-turbo";

    /// <summary>
    /// 触发 AI 对话的快捷键（玩家需站在 NPC 附近按下）。
    /// </summary>
    public KeybindList ToggleKey { get; set; } = new(SButton.L);

    /// <summary>
    /// 与 NPC 互动的最大距离（格）。
    /// </summary>
    public float InteractionRange { get; set; } = 2.5f;

    /// <summary>
    /// 采样温度。
    /// </summary>
    public float Temperature { get; set; } = 0.8f;

    /// <summary>
    /// 单条回复最大 token 数。
    /// </summary>
    public int MaxTokens { get; set; } = 220;

    /// <summary>
    /// 保留的对话历史轮数（用于多轮上下文）。
    /// </summary>
    public int ConversationHistoryLength { get; set; } = 6;

    /// <summary>
    /// 是否在首次对话前向目标接口单独发送一次"人设塑造"请求。
    /// 默认 false：人设作为 system 消息随每条请求一起发送（兼容性更好）。
    /// 设为 true 时会在首次对话前主动发一条 system 消息进行预热。
    /// </summary>
    public bool SendPrimingRequest { get; set; } = false;

    /// <summary>
    /// 请求超时（秒）。
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 额外追加到 system 消息末尾的通用指令（玩家自定义），例如限定回复语言。
    /// </summary>
    public string ExtraSystemInstruction { get; set; } = "请用简短、口语化的中文回复，长度控制在两三句话以内。";

    /// <summary>
    /// 校验配置是否可用。
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(ApiUrl);
}
