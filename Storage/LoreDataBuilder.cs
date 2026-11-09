using System;
using System.Collections.Generic;
using System.Linq;
namespace SV_SOUL.Storage;
public sealed record CharacterLoreSource(string Id,string DisplayName,string BirthSeason,int BirthDay,Dictionary<string,string> Family,bool PublicBirthday=false,string[]? RelatedNpcIds=null);
public sealed record ItemLoreSource(string Id,string Name,string DisplayName,string Description,int Category,bool ExcludeFromRandomSale,bool CanBeGivenAsGift);
public sealed record FestivalLoreSource(string Key,string Name,string Conditions,string DateLabel="",string Rule="",string Asset="");
/// <summary>Pure conversion of resolved effective assets. Does not evaluate dialogue or event scripts.</summary>
public static class LoreDataBuilder
{
    // Everyday produce, animal products, food, gems/minerals, flowers and forage.
    private static readonly HashSet<int> CommonCategories=new(){-75,-79,-80,-81,-26,-27,-5,-6,-7,-2,-12,-4,-15,-16};
    public static List<LoreEntry> Build(IEnumerable<CharacterLoreSource> characters,IEnumerable<ItemLoreSource> objects,IEnumerable<FestivalLoreSource> festivals,string version,string locale)
    {
        var result=new List<LoreEntry>();
        foreach(var c in characters)
        {
            var aliases=new[]{c.Id,c.DisplayName}.Distinct().ToArray();
            var familyKnowers=new[]{c.Id}.Concat(c.RelatedNpcIds ?? Array.Empty<string>()).Distinct().ToArray();
            if(c.BirthDay>0 && c.BirthDay<=28)result.Add(new(){Id="birthday."+c.Id,FactKey="npc."+c.Id+".birthday",Kind="birthday",EntityIds=new[]{c.Id},Text=c.DisplayName+"的生日："+c.BirthSeason+" "+c.BirthDay,Aliases=aliases,Topics=new[]{"生日","哪天出生","birthday","born"},KnownBy=c.PublicBirthday?Array.Empty<string>():familyKnowers,Source="game:Data/Characters/"+c.Id+"/BirthDay"});
            if(c.Family.Count>0)result.Add(new(){Id="family."+c.Id,FactKey="npc."+c.Id+".family",Kind="family",EntityIds=new[]{c.Id},Text=c.DisplayName+"的亲友关系："+string.Join("；",c.Family.Select(r=>r.Key+"："+r.Value)),Aliases=aliases,Topics=new[]{"家人","家庭","父母","妈妈","爸爸","母亲","父亲","兄弟","姐妹","family","parents","mother","father","sister","brother"},KnownBy=familyKnowers,Source="game:Data/Characters/"+c.Id+"/FriendsAndFamily"});
        }
        foreach(var item in objects)
        {
            if(!CommonCategories.Contains(item.Category)||item.ExcludeFromRandomSale||!item.CanBeGivenAsGift||string.IsNullOrWhiteSpace(item.Description))continue;
            // Preserve game's uncertainty in descriptions; never convert flavor text into player experience.
            result.Add(new(){Id="object."+item.Id,FactKey="object."+item.Id+".description",Kind="item",EntityIds=new[]{"(O)"+item.Id},Text=item.DisplayName+"："+item.Description,Aliases=new[]{item.Name,item.DisplayName,"(O)"+item.Id}.Distinct().ToArray(),Source="game:Data/Objects/"+item.Id+"/Description"});
        }
        foreach(var f in festivals)
        {
            if(string.IsNullOrWhiteSpace(f.Name))continue;
            result.Add(new(){Id="calendar."+f.Key,FactKey="festival."+f.Key+".schedule",Kind="calendar",EntityIds=new[]{"festival:"+f.Key},Condition=f.Rule,Text=f.Name+"：游戏日历日期 "+(f.DateLabel==""?f.Key:f.DateLabel)+"；入场地点与时段 "+f.Conditions+"。这是活动安排，不表示玩家已经参加。",Aliases=new[]{f.Name,f.Key},Topics=new[]{"什么时候","几点","哪天","日程","when","time"},Source=f.Asset==""?"game:Data/Festivals/"+f.Key+"/name,conditions":f.Asset});
        }
        foreach(var entry in result){entry.GameVersion=version;entry.Locale=locale;}
        return result;
    }
}
