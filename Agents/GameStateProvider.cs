using System;
using System.Collections.Generic;
using System.Text.Json;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace SV_SOUL.Agents;

public static class GameStateProvider
{
    public static string GetStateJSON(string npcName)
    {
        var state = new Dictionary<string, object>();

        try
        {
            var date = SDate.Now();
            state["season"] = date.Season;
            state["day"] = date.Day;
            state["year"] = date.Year;
            state["timeOfDay"] = Game1.timeOfDay;
            state["isRaining"] = Game1.isRaining;
            state["isSnowing"] = Game1.isSnowing;
            state["isLightning"] = Game1.isLightning;
            state["location"] = Game1.currentLocation?.Name ?? "unknown";
            state["playerName"] = Game1.player?.Name ?? "Farmer";

            if (Game1.player?.friendshipData?.ContainsKey(npcName) == true)
            {
                var friendship = Game1.player.friendshipData[npcName];
                state["friendshipPoints"] = friendship.Points;
                state["isDating"] = friendship.IsDating();
                state["isMarried"] = friendship.IsMarried();
            }
            else
            {
                state["friendshipPoints"] = 0;
                state["isDating"] = false;
                state["isMarried"] = false;
            }
        }
        catch (Exception)
        {
            state["error"] = "Could not read game state";
        }

        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
