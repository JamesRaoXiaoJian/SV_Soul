using System;

namespace SV_SOUL.Storage;

public class ConversationTurn
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
