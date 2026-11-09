using System;
using System.Collections.Generic;

namespace SV_SOUL.Models;

public class NPCPersonality
{
    public string RelationshipMode { get; set; } = "Auto";
    public bool PreserveVanillaInteraction { get; set; } = false;
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Dictionary<string,string> SituationHints { get; set; } = new();
    public Dictionary<string,string[]> SituationGreetings { get; set; } = new();
    public string BehavioralProfile { get; set; } = "";
    public List<DialogueExample> DialogueExamples { get; set; } = new();
    public string[] Greetings { get; set; } = Array.Empty<string>();
    public string ResumeGreeting { get; set; } = "";
    public string CoreTraits { get; set; } = "";
    public string Voice { get; set; } = "";
    public string HistoricalBackground { get; set; } = "";
    public Dictionary<string,string> StageProfiles { get; set; } = new();
    public string SystemPrompt { get; set; } = "";
    public string[] Topics { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> MoodResponses { get; set; } = new();
}
