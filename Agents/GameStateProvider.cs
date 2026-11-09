using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace SV_SOUL.Agents;

public static class GameStateProvider
{
    public static SV_SOUL.Models.WorldContextSnapshot Capture(string npcName)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var json=GetStateJSON(npcName);
        return new(json,watch.Elapsed.TotalMilliseconds);
    }
    public static string GetStateJSON(string npcName)
    {
        var state = new Dictionary<string, object>();

        try
        {
            var date = SDate.Now();
            var season = date.Season.ToString().ToLowerInvariant();
            var npc=Game1.getCharacterFromName(npcName);
            state["canBeRomanced"]=npc?.datable.Value ?? false;
            state["isChild"]=npc?.Age==2;
            state["isPlayerSpouse"]=Game1.player.spouse==npcName;
            state["gameDate"]=$"Y{date.Year} {season} {date.Day} {Game1.timeOfDay}";
            state["npcLocation"]=npc?.currentLocation?.NameOrUniqueName ?? "unknown";
            state["isBirthday"]=npc?.isBirthday() ?? false;
            state["isFestival"]=Utility.isFestivalDay(date.Day,date.Season);
            if(Utility.isFestivalDay(date.Day,date.Season))
            {
                var festival=Game1.content.Load<Dictionary<string,string>>($"Data/Festivals/{season}{date.Day}");
                if(festival.TryGetValue("name",out var name))state["festivalName"]=name;
                if(festival.TryGetValue("conditions",out var conditions))state["festivalLocationAndTime"]=conditions;
            }
            state["npcMoving"]=npc?.isMoving() ?? false;
            state["spouse"]=Game1.player.spouse ?? "none";
            if(Game1.player.spouse==npcName)
            {
                state["farmName"]=Game1.player.farmName.Value;
                state["children"]=Game1.player.getChildren().Select(c=>c.Name).ToArray();
            }
            state["residence"]=Game1.player.spouse==npcName ? "Lives with the player at the farm" : "Not confirmed; current location is not necessarily residence";
            var gameQueries=new List<string>();
            var passiveContexts=new List<SV_SOUL.Models.PassiveFestivalContext>();
            foreach(var pair in Game1.content.Load<Dictionary<string,StardewValley.GameData.PassiveFestivalData>>("Data/PassiveFestivals"))
            {
                var festival=pair.Value;
                bool permitted=string.IsNullOrWhiteSpace(festival.Condition);
                if(!permitted)
                {
                    try { permitted=GameStateQuery.CheckConditions(festival.Condition); } catch { /* Unknown conditions never grant access. */ }
                    if(permitted)gameQueries.Add(festival.Condition);
                }
                var name=string.IsNullOrWhiteSpace(festival.DisplayName)?pair.Key:StardewValley.TokenizableStrings.TokenParser.ParseText(festival.DisplayName);
                passiveContexts.Add(new(name,festival.Season.ToString(),festival.StartDay,festival.EndDay,festival.StartTime,permitted));
            }
            var activeFestivals=SV_SOUL.Models.FestivalContext.Active(passiveContexts,season,date.Day);
            if(activeFestivals.Count>0)
            {
                state["isFestival"]=true;
                var existingName=state.TryGetValue("festivalName",out var currentName)?currentName+"；":"";
                state["festivalName"]=existingName+string.Join("；",activeFestivals.Select(f=>f.Name));
                var existingTime=state.TryGetValue("festivalLocationAndTime",out var currentTime)?currentTime+"；":"";
                state["festivalLocationAndTime"]=existingTime+string.Join("；",activeFestivals.Select(f=>f.Name+" 开始时间 "+f.StartTime));
            }
            state["allowedGameQueries"]=gameQueries;
            state["seenEvents"] = Game1.player.eventsSeen.ToArray();
            state["receivedMail"] = Game1.player.mailReceived.ToArray();
            state["season"] = season;
            state["day"] = date.Day;
            state["year"] = date.Year;
            state["timeOfDay"] = Game1.timeOfDay;
            state["isRaining"] = npc?.currentLocation?.IsRainingHere() ?? Game1.isRaining;
            state["isSnowing"] = Game1.isSnowing;
            state["isLightning"] = Game1.isLightning;
            state["location"] = Game1.currentLocation?.Name ?? "unknown";
            state["playerName"] = Game1.player?.Name ?? "Farmer";

            if (Game1.player?.friendshipData?.ContainsKey(npcName) == true)
            {
                var friendship = Game1.player.friendshipData[npcName];
                state["friendshipPoints"] = friendship.Points;
                state["friendshipHearts"] = Game1.player.getFriendshipHeartLevelForNPC(npcName);
                state["relationship"] = friendship.RoommateMarriage ? "Roommate" : friendship.Status.ToString();
                state["weddingDate"] = friendship.WeddingDate?.ToString() ?? "unknown";
                state["giftsToday"] = friendship.GiftsToday;
                state["giftsThisWeek"] = friendship.GiftsThisWeek;
                state["talkedToday"] = friendship.TalkedToToday;
                state["isDating"] = friendship.IsDating() && !friendship.IsMarried();
                state["isMarried"] = friendship.IsMarried() && !friendship.RoommateMarriage;
            }
            else
            {
                state["relationship"] = "NoFriendshipRecord";
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
