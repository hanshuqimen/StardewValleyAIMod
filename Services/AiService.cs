using System;
using System.Collections.Generic;
using System.IO;
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
/// AI 服务：纯请求中转。mod 自身不做任何模型推理，只把玩家配置的 URL/Key/Model
/// 拼成 OpenAI 兼容的 chat/completions 请求转发出去，再把模型返回的文本交回游戏。
/// </summary>
internal class AiService : IDisposable
{
    private readonly HttpClient _client;
    private readonly ModConfig _config;
    private readonly IMonitor _monitor;

    /// <summary>
    /// 每个 NPC 是否已完成首次"人设预热"请求。
    /// </summary>
    private readonly HashSet<string> _primed = new();

    public AiService(ModConfig config, IMonitor monitor)
    {
        _config = config;
        _monitor = monitor;
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds) };
    }

    /// <summary>
    /// 在首次正式对话前，向目标接口发送一条仅含 system 消息的请求，
    /// 用于"塑造人设"。仅在 config.SendPrimingRequest = true 时调用。
    /// </summary>
    public async Task PrimeAsync(string npcName, string systemPrompt, CancellationToken ct = default)
    {
        if (_primed.Contains(npcName)) return;
        _primed.Add(npcName);

        var body = new
        {
            model = _config.Model,
            messages = new[] { new ChatMessage { Role = "system", Content = systemPrompt } },
            max_tokens = 1
        };
        try
        {
            await PostRawAsync(body, ct).ConfigureAwait(false);
            _monitor.Log($"[AI] 已向 {_config.ApiUrl} 发送 {npcName} 的人设预热请求。", LogLevel.Debug);
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
        if (!_config.IsValid)
            return "(mod 未配置 ApiUrl，请在 config 中填写。)";

        var messages = new List<ChatMessage> { new() { Role = "system", Content = systemPrompt } };
        if (history != null)
            messages.AddRange(history);
        messages.Add(new ChatMessage { Role = "user", Content = userMessage });

        var body = new
        {
            model = _config.Model,
            messages = messages,
            temperature = _config.Temperature,
            max_tokens = _config.MaxTokens
        };

        string json;
        try
        {
            json = await PostRawAsync(body, ct).ConfigureAwait(false);
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
    /// 实际发出 POST 请求并返回响应体字符串。Authorization 头只在这里拼一次。
    /// </summary>
    private async Task<string> PostRawAsync(object body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _config.ApiUrl);
        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _config.ApiKey);
        req.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var resp = await _client.SendAsync(req, ct).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(text)}");
        return text;
    }

    /// <summary>
    /// 从 OpenAI 兼容响应里提取 choices[0].message.content。
    /// </summary>
    private static string ParseAssistantContent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
            {
                return content.GetString()?.Trim() ?? "";
            }
            // 部分 API 用 text 字段
            if (first.TryGetProperty("text", out var textEl))
                return textEl.GetString()?.Trim() ?? "";
        }
        return "(AI 没有返回内容。)";
    }

    private static string Truncate(string s, int n = 300)
        => s.Length <= n ? s : s.Substring(0, n) + "...";

    public void Dispose() => _client.Dispose();
}
