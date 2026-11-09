namespace SV_SOUL.Config;

public class ModConfig
{
    public string ApiProvider { get; set; } = "Anthropic";
    public bool UseMaxCompletionTokens { get; set; } = true;
    public bool SendTemperature { get; set; } = true;
    public string ApiKey { get; set; } = "";
    public string? BaseUrl { get; set; } = null;
    public string[] EnabledNPCs { get; set; } = new[] { "*" };
    public string[] ExcludedNPCs { get; set; } = System.Array.Empty<string>();
    public string ControllerChatButton { get; set; } = "ControllerY";
    public string SocialChatKey { get; set; } = "LeftAlt";
    public int MaxTokens { get; set; } = 200;
    public float Temperature { get; set; } = 0.9f;
    public int MaxHistoryTurns { get; set; } = 8;
    public string? TriggerKey { get; set; } = null;
    public string Model { get; set; } = "mimo-v2.5-pro";
    public string Language { get; set; } = "zh";
    public bool EnableTestBridge { get; set; } = false;
    public bool DebugMode { get; set; } = false;
}
