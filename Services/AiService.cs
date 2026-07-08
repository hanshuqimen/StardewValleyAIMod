using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using StardewModdingAPI;

namespace StardewValleyAIMod.Services;

/// <summary>
/// 一条对话消息（OpenAI chat 格式）。
/// </summary>
internal class ChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
}

/// <summary>
/// AI 服务：纯请求中转。mod 自身不做任何模型推理，只把玩家在设置窗口填写的
/// URL/Key/Model 拼成 OpenAI 兼容的 chat/completions 请求转发出去，再把模型返回的
/// 文本交回游戏。Authorization 头现拼现发，不缓存密钥。
/// </summary>
internal class AiService : IDisposable
{
    private readonly ModSettings _settings;
    private readonly IMonitor _monitor;

    /// <summary>每个 NPC 是否已完成首次"人设预热"请求。</summary>
    private readonly HashSet<string> _primed = new();

    public AiService(ModSettings settings, IMonitor monitor)
    {
        _settings = settings;
        _monitor = monitor;
    }

    /// <summary>
    /// 在首次正式对话前，向目标接口发送一条仅含 system 消息的请求，用于"塑造人设"。
    /// 仅在 <see cref="ModSettings.SendPrimingRequest"/> = true 时调用。
    /// </summary>
    public async Task PrimeAsync(string npcName, string systemPrompt, CancellationToken ct = default)
    {
        if (_primed.Contains(npcName)) return;
        _primed.Add(npcName);

        var body = new
        {
            model = _settings.Model,
            messages = new[] { new ChatMessage { Role = "system", Content = systemPrompt } },
            max_tokens = 1
        };
        try
        {
            await PostRawAsync(body, _settings.ApiUrl, _settings.ApiKey, ct).ConfigureAwait(false);
            _monitor.Log($"[AI] 已向 {_settings.ApiUrl} 发送 {npcName} 的人设预热请求。", LogLevel.Debug);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _monitor.Log($"[AI] {npcName} 人设预热请求失败（已忽略，将继续正常对话）：{ex.Message}", LogLevel.Warn);
        }
    }

    /// <summary>
    /// 发送一轮对话并把模型回复文本返回。
    /// <paramref name="history"/> 为之前的轮次（role=user/assistant 交替）。
    /// </summary>
    public async Task<string> SendAsync(
        string npcName,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
    {
        if (!_settings.IsValid)
            return "(尚未配置 AI 网址，请按 K 打开设置窗口填写。)";

        var messages = new List<ChatMessage> { new() { Role = "system", Content = systemPrompt } };
        if (history != null)
            messages.AddRange(history);
        messages.Add(new ChatMessage { Role = "user", Content = userMessage });

        var body = new
        {
            model = _settings.Model,
            messages = messages,
            temperature = _settings.Temperature,
            max_tokens = _settings.MaxTokens
        };

        string json;
        try
        {
            json = await PostRawAsync(body, _settings.ApiUrl, _settings.ApiKey, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return "（请求超时了，稍后再试。）";
        }
        catch (Exception ex)
        {
            _monitor.Log($"[AI] 请求失败：{ex.Message}", LogLevel.Error);
            return $"（请求出错：{ex.Message}）";
        }

        return ParseAssistantContent(json);
    }

    /// <summary>
    /// 用玩家在设置窗口临时填写的网址/Key/模型发一条最小请求，验证连通性。
    /// 不会动用已保存设置，便于玩家"先测试再保存"。
    /// </summary>
    public async Task<(bool Ok, string Message)> TestAsync(
        string url, string key, string model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (false, "请先填写 AI 网址。");

        var body = new
        {
            model = string.IsNullOrWhiteSpace(model) ? "gpt-3.5-turbo" : model,
            messages = new[]
            {
                new ChatMessage { Role = "system", Content = "ping" },
                new ChatMessage { Role = "user", Content = "1" }
            },
            max_tokens = 1
        };
        try
        {
            var json = await PostRawAsync(body, url, key, ct).ConfigureAwait(false);
            // 只要返回 200 且能解析就认为成功
            _ = ParseAssistantContent(json);
            return (true, "连接成功！可以保存后开始对话。");
        }
        catch (OperationCanceledException)
        {
            return (false, "请求超时，请检查网址或网络。");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>实际发出 POST 请求并返回响应体字符串。Authorization 头只在这里拼一次。</summary>
    private static async Task<string> PostRawAsync(object body, string url, string key, CancellationToken ct)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrWhiteSpace(key))
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
        req.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(text)}");
        return text;
    }

    /// <summary>从 OpenAI 兼容响应里提取 choices[0].message.content。</summary>
    private static string ParseAssistantContent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
                return content.GetString()?.Trim() ?? "";
            if (first.TryGetProperty("text", out var textEl))
                return textEl.GetString()?.Trim() ?? "";
        }
        return "(AI 没有返回内容。)";
    }

    private static string Truncate(string s, int n = 300)
        => s.Length <= n ? s : s.Substring(0, n) + "...";

    public void Dispose() { }
}
