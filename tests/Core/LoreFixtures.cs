using System.Text.Json;
using System.Text.RegularExpressions;
using SV_SOUL.Storage;
/// <summary>Optional integration fixture: reads locally extracted licensed assets, never starts the game.</summary>
public static class LoreFixtures
{
    public static List<LoreEntry> LoadNative(string folder)
    {
        JsonDocument Read(string name)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,name+"-raw.json")));
        using var names=Read("npcnames");using var objects=Read("objects");using var objectStrings=Read("objectstrings");using var familyStrings=Read("characterstrings");using var characters=Read("catalog");using var dates=Read("festivaldates");using var passive=Read("passivefestivals");
        string Name(string id)=>names.RootElement.TryGetProperty(id.Replace(" ",""),out var v)?v.GetString()!:id;
        string Resolve(string value)=>Regex.Replace(value,@"\[LocalizedText Strings\\(\w+):([^\]]+)\]",m=>
        {
            var source=m.Groups[1].Value switch{"Objects"=>objectStrings.RootElement,"Characters"=>familyStrings.RootElement,"NPCNames"=>names.RootElement,_=>default};
            return source.ValueKind==JsonValueKind.Object&&source.TryGetProperty(m.Groups[2].Value,out var v)?v.GetString()!:m.Value;
        });
        var seasons=new[]{"Spring","Summer","Fall","Winter"};
        var people=new List<CharacterLoreSource>();
        foreach(var row in characters.RootElement.EnumerateArray())
        {
            var id=row.GetProperty("Name").GetString()!;var d=row.GetProperty("Data");var family=new Dictionary<string,string>();
            if(d.TryGetProperty("FriendsAndFamily",out var f)&&f.ValueKind==JsonValueKind.Object)foreach(var p in f.EnumerateObject())family[Name(p.Name)]=Resolve(p.Value.GetString()!);
            var season=d.GetProperty("BirthSeason");people.Add(new(id,Name(id),season.ValueKind==JsonValueKind.Number?seasons[season.GetInt32()]:"unknown",d.GetProperty("BirthDay").GetInt32(),family,d.GetProperty("Calendar").GetInt32()==0 && (!d.TryGetProperty("UnlockConditions",out var unlock)||unlock.ValueKind==JsonValueKind.Null||unlock.GetString()==""),f.ValueKind==JsonValueKind.Object?f.EnumerateObject().Select(p=>p.Name).ToArray():Array.Empty<string>()));
        }
        var items=objects.RootElement.EnumerateObject().Select(p=>
        {
            var d=p.Value;return new ItemLoreSource(p.Name,d.GetProperty("Name").GetString()!,Resolve(d.GetProperty("DisplayName").GetString()!),Resolve(d.GetProperty("Description").GetString()!),d.GetProperty("Category").GetInt32(),d.GetProperty("ExcludeFromRandomSale").GetBoolean(),d.GetProperty("CanBeGivenAsGift").GetBoolean());
        }).ToList();
        var holidays=new List<FestivalLoreSource>();
        foreach(var d in dates.RootElement.EnumerateObject())
        {
            using var f=Read("festival-"+d.Name);holidays.Add(new(d.Name,f.RootElement.GetProperty("name").GetString()!,f.RootElement.GetProperty("conditions").GetString()!));
        }
        foreach(var d in passive.RootElement.EnumerateObject())
        {
            var f=d.Value;var condition=f.GetProperty("Condition").GetString()!;
            holidays.Add(new(d.Name,d.Name,"开始时间 "+f.GetProperty("StartTime"),seasons[f.GetProperty("Season").GetInt32()]+" "+f.GetProperty("StartDay")+"–"+f.GetProperty("EndDay"),condition==""?"":"gamequery:"+condition,"game:Data/PassiveFestivals/"+d.Name));
        }
        return LoreDataBuilder.Build(people,items,holidays,"1.6.15","zh-CN");
    }
}
