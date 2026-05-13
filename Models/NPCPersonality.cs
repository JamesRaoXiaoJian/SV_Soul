using System;
using System.Collections.Generic;

namespace SV_SOUL.Models;

public class NPCPersonality
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public string[] Topics { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> MoodResponses { get; set; } = new();
}
