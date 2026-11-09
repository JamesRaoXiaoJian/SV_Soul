using System;

namespace SV_SOUL.Storage;

public class ConversationTurn
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string GameTime { get; set; } = "unknown";
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
