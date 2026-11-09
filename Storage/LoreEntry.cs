using System;
namespace SV_SOUL.Storage;
public sealed class LoreEntry
{
    public string Id { get; set; } = "";
    public string FactKey { get; set; } = "";
    public string Kind { get; set; } = "background";
    public string[] EntityIds { get; set; } = Array.Empty<string>();
    public string Text { get; set; } = "";
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string[] Topics { get; set; } = Array.Empty<string>();
    public string Source { get; set; } = "";
    public string ReviewedAt { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public string Locale { get; set; } = "";
    public string[] KnownBy { get; set; } = Array.Empty<string>();
    public string Condition { get; set; } = "";
    public string[] RequiredAll { get; set; } = Array.Empty<string>();
    public string[] RequiredAny { get; set; } = Array.Empty<string>();
}
