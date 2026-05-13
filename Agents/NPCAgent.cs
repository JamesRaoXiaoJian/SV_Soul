using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SV_SOUL.Config;
using SV_SOUL.Models;
using StardewModdingAPI;
using SV_SOUL.Storage;

namespace SV_SOUL.Agents;

public class NPCAgent
{
    private readonly NPCPersonality _personality;
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly StorageManager _storage;
    private readonly ModConfig _config;
    private readonly IMonitor _monitor;
    private readonly List<(string Role, string Content)> _messages = new();
    private int _turnCount;

    private const string ApiVersion = "2023-06-01";

    public string Name => _personality.Name;
    public string DisplayName => _personality.DisplayName;
    public NPCPersonality Personality => _personality;

    public NPCAgent(NPCPersonality personality, HttpClient http, string apiKey, string baseUrl, StorageManager storage, ModConfig config, IMonitor monitor)
    {
        _personality = personality;
        _http = http;
        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
        _storage = storage;
        _config = config;
        _monitor = monitor;

        LoadHistory();
    }

    public string BuildSystemPrompt()
    {
        var gameState = GameStateProvider.GetStateJSON(_personality.Name);
        var memories = _storage.GetMemoriesSummary(_personality.Name);

        var parts = new List<string>
        {
            _personality.SystemPrompt,
            "",
            "Current game state:",
            gameState
        };

        if (!string.IsNullOrEmpty(memories))
        {
            parts.Add("");
            parts.Add("What you remember about the player:");
            parts.Add(memories);
        }

        parts.Add("");
        parts.Add("Stay in character at all times. Respond as this NPC would, referencing the current game state and your memories naturally. Keep responses concise (1-3 sentences unless the conversation calls for more). Do not break the fourth wall or reference being an AI.");

        return string.Join("\n", parts);
    }

    public async Task<string> SendAsync(string playerText)
    {
        _monitor.Log($"[{_personality.Name}] Player: {playerText}", LogLevel.Debug);
        _messages.Add(("user", playerText));

        var systemPrompt = BuildSystemPrompt();

        try
        {
            var assistantText = await CallApiAsync(systemPrompt, _config.MaxTokens);

            _monitor.Log($"[{_personality.Name}] NPC: {assistantText}", LogLevel.Debug);
            _messages.Add(("assistant", assistantText));
            _turnCount++;

            SaveHistory();
            PruneIfNeeded();

            if (_turnCount % 10 == 0)
                await ExtractMemoriesAsync();

            return assistantText;
        }
        catch (Exception ex)
        {
            _monitor.Log($"[{_personality.Name}] Send failed: {ex.Message}", LogLevel.Error);
            _messages.RemoveAt(_messages.Count - 1);
            var errorMsg = CleanErrorMessage(ex.Message);
            return $"({_personality.DisplayName} seems distracted... {errorMsg})";
        }
    }

    private async Task<string> CallApiAsync(string systemPrompt, int maxTokens)
    {
        var messagesArray = _messages.Select(m => new Dictionary<string, object>
        {
            ["role"] = m.Role,
            ["content"] = m.Content
        }).ToList();

        var body = new Dictionary<string, object>
        {
            ["model"] = _config.Model,
            ["max_tokens"] = maxTokens,
            ["system"] = systemPrompt,
            ["messages"] = messagesArray
        };

        var json = JsonSerializer.Serialize(body);
        var url = $"{_baseUrl}/v1/messages";

        _monitor.Log($"[{_personality.Name}] API request -> {url} model={_config.Model} msgs={_messages.Count} maxTokens={maxTokens}", LogLevel.Debug);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        _monitor.Log($"[{_personality.Name}] API response: {response.StatusCode} (len={responseJson.Length})", LogLevel.Debug);

        if (!response.IsSuccessStatusCode)
        {
            _monitor.Log($"[{_personality.Name}] API error: {response.StatusCode} - {responseJson}", LogLevel.Error);
            throw new Exception($"API {response.StatusCode}: {responseJson}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
        {
            var first = content[0];
            if (first.TryGetProperty("text", out var text))
                return text.GetString() ?? "...";
        }

        return "...";
    }

    private void LoadHistory()
    {
        var turns = _storage.LoadHistory(_personality.Name);
        foreach (var turn in turns)
            _messages.Add((turn.Role, turn.Content));
        _turnCount = turns.Count / 2;
    }

    private void SaveHistory()
    {
        var turns = _messages.Select(m => new ConversationTurn
        {
            Role = m.Role,
            Content = m.Content,
            Timestamp = DateTime.Now
        }).ToList();
        _storage.SaveHistory(_personality.Name, turns);
    }

    private void PruneIfNeeded()
    {
        var maxMessages = _config.MaxHistoryTurns * 2;
        if (_messages.Count <= maxMessages)
            return;

        var toRemove = _messages.Count - maxMessages;
        if (toRemove % 2 != 0) toRemove++;
        _messages.RemoveRange(0, toRemove);
    }

    private static string CleanErrorMessage(string raw)
    {
        try
        {
            // Try to extract "message" field from JSON error response
            var idx = raw.IndexOf("\"message\"", StringComparison.Ordinal);
            if (idx >= 0)
            {
                var start = raw.IndexOf('"', idx + 9);
                if (start >= 0)
                {
                    start++;
                    var end = raw.IndexOf('"', start);
                    if (end > start)
                        return raw.Substring(start, end - start);
                }
            }
            // Fallback: truncate raw message
            return raw.Length > 120 ? raw.Substring(0, 120) + "..." : raw;
        }
        catch
        {
            return raw.Length > 120 ? raw.Substring(0, 120) + "..." : raw;
        }
    }

    private async Task ExtractMemoriesAsync()
    {
        try
        {
            var recentHistory = string.Join("\n", _messages.TakeLast(20).Select(m =>
                $"{(m.Role == "user" ? "Player" : _personality.DisplayName)}: {m.Content}"));

            var metaPrompt = $@"Based on this conversation, extract key facts about the player that {_personality.DisplayName} would remember. Return a JSON array of short string facts (e.g., [""likes fishing"", ""gave me a diamond on Spring 14""]). Only include NEW information not obvious from the game. Return ONLY the JSON array, nothing else.

Conversation:
{recentHistory}";

            var savedMessages = _messages.ToList();
            _messages.Clear();
            _messages.Add(("user", metaPrompt));

            var json = await CallApiAsync("", 300);

            _messages.Clear();
            _messages.AddRange(savedMessages);

            var facts = JsonSerializer.Deserialize<string[]>(json);
            if (facts is { Length: > 0 })
            {
                var memory = _storage.LoadMemories(_personality.Name);
                foreach (var fact in facts)
                {
                    if (!memory.KeyFacts.Contains(fact))
                        memory.KeyFacts.Add(fact);
                }
                _storage.SaveMemories(_personality.Name, memory);
            }
        }
        catch
        {
            // Memory extraction is best-effort; don't break the conversation
        }
    }
}
