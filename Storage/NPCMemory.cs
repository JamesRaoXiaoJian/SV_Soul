using System;
using System.Collections.Generic;

namespace SV_SOUL.Storage;

public class NPCMemory
{
    public List<string> KeyFacts { get; set; } = new();
    public Dictionary<string, string> RelationshipMilestones { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
