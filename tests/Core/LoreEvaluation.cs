using System.Text.Json;
using SV_SOUL.Agents;
using SV_SOUL.Config;
using SV_SOUL.Models;
using SV_SOUL.Storage;
public static class LoreEvaluation
{
    public static void Export(string path)
    {
        var cards=Directory.GetFiles("assets/personalities","*.json").Select(p=>JsonSerializer.Deserialize<NPCPersonality>(File.ReadAllText(p))!).ToDictionary(p=>p.Name);
        var entries=Directory.GetFiles("assets/lore","*.json").SelectMany(p=>JsonSerializer.Deserialize<List<LoreEntry>>(File.ReadAllText(p))!).Concat(LoreFixtures.LoadNative("tools"));
        using var index=new LoreRetriever(entries,"1.6.15");
        var cases=new[]{
            ("Abigail","你妈妈是谁？", "{\"relationship\":\"Married\",\"isPlayerSpouse\":true,\"friendshipHearts\":12,\"residence\":\"Lives with the player at the farm\"}"),
            ("Abigail","你现在还住在皮埃尔家里吗？","{\"relationship\":\"Married\",\"isPlayerSpouse\":true,\"friendshipHearts\":12,\"residence\":\"Lives with the player at the farm\"}"),
            ("Leah","今天什么都没做好，我有点难过。","{\"relationship\":\"Friendly\",\"friendshipHearts\":6}"),
            ("Willy","鳟鱼大赛什么时候开始？","{\"relationship\":\"Friendly\",\"friendshipHearts\":4}"),
            ("Wizard","你能告诉我元素战争的详细经过吗？","{\"relationship\":\"Friendly\",\"friendshipHearts\":0}"),
            ("Wizard","元素战争是哪两方之间的事？","{\"relationship\":\"Friendly\",\"friendshipHearts\":6}"),
            ("Sandy","沙漠节什么时候举行？","{\"relationship\":\"Friendly\",\"friendshipHearts\":4,\"allowedGameQueries\":[\"LOCATION_ACCESSIBLE Desert\"]}"),
            ("Penny","紫水晶是什么？","{\"relationship\":\"Friendly\",\"friendshipHearts\":3}")
        };
        var output=cases.Select(c=>
        {
            var agent=new NPCAgent(cards[c.Item1],new HttpClient(),"unused","https://example.test/v1",new StorageManager(new SaveMemory()),new ModConfig(),new Monitor()){Lore=index};
            return new{npc=c.Item1,user=c.Item2,relationship=c.Item3,system=agent.BuildSystemPrompt(c.Item3,c.Item2),sources=index.Search(c.Item2,LoreQueryContext.FromSnapshot(c.Item1,c.Item3)).Select(h=>h.Id).ToArray()};
        }).ToArray();
        File.WriteAllText(path,JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
    }
}
