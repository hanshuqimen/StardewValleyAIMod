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

internal class ChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
}

internal class AiService : IDisposable
{
    private readonly ModSettings _settings;
    private readonly IMonitor _monitor;

    private readonly HashSet<string> _primed = new();

    public AiService(ModSettings settings, IMonitor monitor)
    {
        _settings = settings;
        _monitor = monitor;
    }

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
