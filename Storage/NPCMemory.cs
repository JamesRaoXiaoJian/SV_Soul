using System;
using System.Collections.Generic;

namespace SV_SOUL.Storage;

public class NPCMemory
{
    public int ExtractedThrough { get; set; }
    public List<MemoryRecord> Records { get; set; } = new();
    public string LastWorldState { get; set; } = "";
    public List<string> KeyFacts { get; set; } = new();
    public Dictionary<string, string> RelationshipMilestones { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
