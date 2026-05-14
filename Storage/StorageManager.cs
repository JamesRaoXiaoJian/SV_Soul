using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SV_SOUL.Storage;

public class StorageManager
{
    private readonly string _dataDir;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public StorageManager(string modDir)
    {
        _dataDir = Path.Combine(modDir, "data");
        Directory.CreateDirectory(_dataDir);
    }

    public List<ConversationTurn> LoadHistory(string npcName)
    {
        var path = GetHistoryPath(npcName);
        if (!File.Exists(path))
            return new List<ConversationTurn>();

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new List<ConversationTurn>();
            return JsonSerializer.Deserialize<List<ConversationTurn>>(json, JsonOptions) ?? new List<ConversationTurn>();
        }
        catch
        {
            return new List<ConversationTurn>();
        }
    }

    public void SaveHistory(string npcName, List<ConversationTurn> turns)
    {
        var path = GetHistoryPath(npcName);
        var json = JsonSerializer.Serialize(turns, JsonOptions);
        File.WriteAllText(path, json);
    }

    public NPCMemory LoadMemories(string npcName)
    {
        var path = GetMemoryPath(npcName);
        if (!File.Exists(path))
            return new NPCMemory();

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new NPCMemory();
            return JsonSerializer.Deserialize<NPCMemory>(json, JsonOptions) ?? new NPCMemory();
        }
        catch
        {
            return new NPCMemory();
        }
    }

    public void SaveMemories(string npcName, NPCMemory memory)
    {
        memory.LastUpdated = DateTime.Now;
        var path = GetMemoryPath(npcName);
        var json = JsonSerializer.Serialize(memory, JsonOptions);
        File.WriteAllText(path, json);
    }

    public string GetMemoriesSummary(string npcName)
    {
        var memory = LoadMemories(npcName);
        if (memory.KeyFacts.Count == 0 && memory.RelationshipMilestones.Count == 0)
            return "";

        var parts = new List<string>();
        if (memory.KeyFacts.Count > 0)
        {
            parts.Add("Key facts about the player:");
            foreach (var fact in memory.KeyFacts.TakeLast(20))
                parts.Add($"- {fact}");
        }
        if (memory.RelationshipMilestones.Count > 0)
        {
            parts.Add("Relationship milestones:");
            foreach (var (date, evt) in memory.RelationshipMilestones.TakeLast(10))
                parts.Add($"- [{date}] {evt}");
        }
        return string.Join("\n", parts);
    }

    private string GetHistoryPath(string npcName) =>
        Path.Combine(_dataDir, $"{npcName.ToLower()}_history.json");

    private string GetMemoryPath(string npcName) =>
        Path.Combine(_dataDir, $"{npcName.ToLower()}_memory.json");
}
