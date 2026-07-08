using System.Collections.Generic;
using StardewValleyAIMod.Services;

namespace StardewValleyAIMod.Services;

/// <summary>
/// 管理每个 NPC 的对话历史，供多轮上下文使用。
/// </summary>
internal class ConversationStore
{
    private readonly ModConfig _config;
    private readonly Dictionary<string, List<ChatMessage>> _history = new();

    public ConversationStore(ModConfig config) => _config = config;

    /// <summary>
    /// 取出某 NPC 的历史（只读副本，调用方可安全使用）。
    /// </summary>
    public IReadOnlyList<ChatMessage> Get(string npcName)
    {
        if (!_history.TryGetValue(npcName, out var list)) return System.Array.Empty<ChatMessage>();
        return list.ToArray();
    }

    /// <summary>
    /// 追加一轮 user/assistant 消息，并按配置裁剪长度。
    /// </summary>
    public void Append(string npcName, string userText, string assistantText)
    {
        if (!_history.TryGetValue(npcName, out var list))
            _history[npcName] = list = new List<ChatMessage>();

        list.Add(new ChatMessage { Role = "user", Content = userText });
        list.Add(new ChatMessage { Role = "assistant", Content = assistantText });

        var keep = _config.ConversationHistoryLength * 2;
        if (list.Count > keep && keep > 0)
            list.RemoveRange(0, list.Count - keep);
    }

    public void Clear(string npcName)
    {
        if (_history.ContainsKey(npcName)) _history[npcName].Clear();
    }
}
