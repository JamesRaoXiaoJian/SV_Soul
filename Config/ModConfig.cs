namespace SV_SOUL.Config;

public class ModConfig
{
    public string ApiKey { get; set; } = "";
    public string? BaseUrl { get; set; } = null;
    public string[] EnabledNPCs { get; set; } = new[] { "Abigail", "Shane", "Sebastian", "Elliott", "Haley" };
    public int MaxTokens { get; set; } = 200;
    public float Temperature { get; set; } = 0.9f;
    public int MaxHistoryTurns { get; set; } = 50;
    public string? TriggerKey { get; set; } = null;
    public string Model { get; set; } = "mimo-v2.5-pro";
    public bool DebugMode { get; set; } = false;
}
