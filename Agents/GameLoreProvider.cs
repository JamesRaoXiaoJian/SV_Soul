using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Objects;
using SV_SOUL.Models;
using SV_SOUL.Storage;
using StardewValley.TokenizableStrings;
namespace SV_SOUL.Agents;
/// <summary>Main-thread only. Copy resolved values; never pass game asset objects to workers.</summary>
public static class GameLoreProvider
{
    public static List<LoreEntry> Capture(IModHelper helper, Dictionary<string,NPCPersonality> profiles)
    {
        var data=helper.GameContent.Load<Dictionary<string,CharacterData>>("Data/Characters");
        var names=data.ToDictionary(p=>p.Key,p=>TokenParser.ParseText(p.Value.DisplayName));
        var characters=data.Select(p=>new CharacterLoreSource(p.Key,names[p.Key],p.Value.BirthSeason?.ToString() ?? "unknown",p.Value.BirthDay,(p.Value.FriendsAndFamily??new()).ToDictionary(r=>names.GetValueOrDefault(r.Key,r.Key),r=>TokenParser.ParseText(r.Value)),p.Value.Calendar==CalendarBehavior.AlwaysShown && string.IsNullOrWhiteSpace(p.Value.UnlockConditions),p.Value.FriendsAndFamily?.Keys.ToArray())).ToArray();
        var items=helper.GameContent.Load<Dictionary<string,ObjectData>>("Data/Objects").Select(p=>new ItemLoreSource(p.Key,p.Value.Name,TokenParser.ParseText(p.Value.DisplayName),TokenParser.ParseText(p.Value.Description),p.Value.Category,p.Value.ExcludeFromRandomSale,p.Value.CanBeGivenAsGift)).ToArray();
        var dates=helper.GameContent.Load<Dictionary<string,string>>("Data/Festivals/FestivalDates");
        var festivals=new List<FestivalLoreSource>();
        foreach(var date in dates)
        {
            var asset="Data/Festivals/"+date.Key;
            if(!helper.GameContent.DoesAssetExist<Dictionary<string,string>>(helper.GameContent.ParseAssetName(asset)))continue;
            var festival=helper.GameContent.Load<Dictionary<string,string>>(asset);
            festivals.Add(new(date.Key,festival.GetValueOrDefault("name",date.Value),festival.GetValueOrDefault("conditions","unknown")));
        }
        var passive=helper.GameContent.Load<Dictionary<string,StardewValley.GameData.PassiveFestivalData>>("Data/PassiveFestivals");
        foreach(var pair in passive)
        {
            var f=pair.Value;
            if(!f.ShowOnCalendar && pair.Key!="TroutDerby" && pair.Key!="SquidFest")continue;
            var name=string.IsNullOrWhiteSpace(f.DisplayName)?pair.Key:TokenParser.ParseText(f.DisplayName);
            festivals.Add(new(pair.Key,name,"开始时间 "+f.StartTime+"；是否可参加以当前游戏为准",f.Season+" "+f.StartDay+"–"+f.EndDay,string.IsNullOrWhiteSpace(f.Condition)?"":"gamequery:"+f.Condition,"game:Data/PassiveFestivals/"+pair.Key));
        }
        return LoreDataBuilder.Build(characters,items,festivals,StardewValley.Game1.version,helper.GameContent.CurrentLocale);
    }
}
