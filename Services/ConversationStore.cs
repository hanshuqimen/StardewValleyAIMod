using System.Collections.Generic;

namespace StardewValleyAIMod.Services;

internal class ConversationStore
{
    private readonly ModSettings _settings;
    private readonly Dictionary<string, List<ChatMessage>> _history = new();

    public ConversationStore(ModSettings settings) => _settings = settings;

    public IReadOnlyList<ChatMessage> Get(string npcName)
    {
        if (!_history.TryGetValue(npcName, out var list)) return System.Array.Empty<ChatMessage>();
        return list.ToArray();
    }

    public void Append(string npcName, string userText, string assistantText)
    {
        if (!_history.TryGetValue(npcName, out var list))
            _history[npcName] = list = new List<ChatMessage>();

        list.Add(new ChatMessage { Role = "user", Content = userText });
        list.Add(new ChatMessage { Role = "assistant", Content = assistantText });

        var keep = _settings.ConversationHistoryLength * 2;
        if (list.Count > keep && keep > 0)
            list.RemoveRange(0, list.Count - keep);
    }

    public void Clear(string npcName)
    {
        if (_history.ContainsKey(npcName)) _history[npcName].Clear();
    }
}
