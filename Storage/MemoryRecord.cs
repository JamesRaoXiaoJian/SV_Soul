using System;
using System.Collections.Generic;
namespace SV_SOUL.Storage;
public class MemoryScope
{
    public string SaveId { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string NpcId { get; set; } = "";
}
public class MemoryRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "PlayerStatement";
    public string Key { get; set; } = "";
    public string Text { get; set; } = "";
    public string GameTime { get; set; } = "unknown";
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public List<string> EvidenceIds { get; set; } = new();
    public int Importance { get; set; } = 1;
    public string? ValidUntil { get; set; }
    public bool Active { get; set; } = true;
    public string? SupersededBy { get; set; }
}
public class SaveMemory
{
    public int SchemaVersion { get; set; } = 2;
    public string SaveId { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public Dictionary<string, List<ConversationTurn>> Conversations { get; set; } = new();
    public Dictionary<string, NPCMemory> Npcs { get; set; } = new();
}
