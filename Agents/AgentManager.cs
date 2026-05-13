using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using SV_SOUL.Config;
using SV_SOUL.Models;
using SV_SOUL.Storage;
using SV_SOUL.UI;

namespace SV_SOUL.Agents;

public class AgentManager
{
    private readonly Dictionary<string, NPCAgent> _agents = new();
    private readonly Dictionary<string, NPCPersonality> _personalities = new();
    private readonly HttpClient _http = new();
    private readonly StorageManager _storage;
    private readonly ModConfig _config;
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;

    public AgentManager(IModHelper helper, IMonitor monitor, ModConfig config)
    {
        _helper = helper;
        _monitor = monitor;
        _config = config;
        _storage = new StorageManager(helper.DirectoryPath);

        LoadPersonalities();
    }

    private void LoadPersonalities()
    {
        foreach (var npcName in _config.EnabledNPCs)
        {
            var fileName = $"{npcName.ToLower()}.json";
            var assetPath = Path.Combine("assets", "personalities", fileName);

            try
            {
                var fullPath = Path.Combine(_helper.DirectoryPath, assetPath);
                if (File.Exists(fullPath))
                {
                    var json = File.ReadAllText(fullPath);
                    var personality = System.Text.Json.JsonSerializer.Deserialize<NPCPersonality>(json);
                    if (personality != null)
                    {
                        _personalities[npcName] = personality;
                        _monitor.Log($"Loaded personality for {npcName}", LogLevel.Debug);
                    }
                }
                else
                {
                    _monitor.Log($"Personality file not found: {assetPath}", LogLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to load personality for {npcName}: {ex.Message}", LogLevel.Error);
            }
        }
    }

    public bool IsConfiguredNPC(string npcName)
    {
        return _personalities.ContainsKey(npcName);
    }

    public void OpenChat(string npcName, Action<string, string> onResponse)
    {
        if (!_personalities.TryGetValue(npcName, out var personality))
        {
            _monitor.Log($"No personality configured for {npcName}", LogLevel.Warn);
            return;
        }

        if (!_agents.TryGetValue(npcName, out var agent))
        {
            _monitor.Log($"Creating agent for {npcName} (model={_config.Model}, baseUrl={_config.BaseUrl})", LogLevel.Info);
            agent = new NPCAgent(personality, _http, _config.ApiKey, _config.BaseUrl ?? "https://api.anthropic.com", _storage, _config, _monitor);
            _agents[npcName] = agent;
        }

        var npc = Game1.getCharacterFromName(npcName);
        var portrait = npc?.Portrait;

        _monitor.Log($"Opening chat with {npcName}", LogLevel.Debug);
        Game1.activeClickableMenu = new ChatMenu(agent, portrait, _monitor, (npcNameArg, playerText) =>
        {
            Task.Run(async () =>
            {
                try
                {
                    var response = await agent.SendAsync(playerText);
                    onResponse(npcNameArg, response);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"API error for {npcNameArg}: {ex.Message}", LogLevel.Error);
                    onResponse(npcNameArg, $"({agent.DisplayName} seems to have zoned out...)");
                }
            });
        });
    }

    public void FlushAll()
    {
        _agents.Clear();
    }
}
