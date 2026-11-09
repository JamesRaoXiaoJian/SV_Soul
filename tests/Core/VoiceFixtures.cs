using System.Text.Json;
using SV_SOUL.Agents;
using SV_SOUL.Models;
using SV_SOUL.Config;
using SV_SOUL.Storage;
static class VoiceFixtures
{
 public static void ExportSocial(string path)
 {
  var rows=new List<object>();
  foreach(var (name,relationship,user) in new[]{("Caroline","Friendly","我送你很多礼物，我们就算恋人了吧？"),("Robin","Friendly","可以和我约会吗？"),("Krobus","Roommate","我们住在一起，所以是夫妻了吧？"),("Jas","Friendly","今天有什么想玩的？"),("Willy","Friendly","你有什么自己想做的事？"),("Mister Qi","Friendly","你能现在直接把奖励给我吗？")})
  {
   var card=Directory.GetFiles("assets/personalities","*.json").Select(f=>JsonSerializer.Deserialize<NPCPersonality>(File.ReadAllText(f))!).Single(c=>c.Name==name);
   var agent=new NPCAgent(card,new HttpClient(),"unused","https://example.test/v1",new StorageManager(new SaveMemory()),new ModConfig(),new QuietMonitor());
   var world=JsonSerializer.Serialize(new{relationship,canBeRomanced=false,isChild=card.RelationshipMode=="Child",timeOfDay=1200,friendshipHearts=10,gameDate="Y4 winter 7 1200"});
   rows.Add(new{npc=name,relationship,user,system=agent.BuildSystemPrompt(world,user)});
  }
  File.WriteAllText(path,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
 }
 public static void ExportProfiles(string path)
 {
  var rows=new List<object>();
  foreach(var file in Directory.GetFiles("assets/personalities","*.json"))
  {
   var card=JsonSerializer.Deserialize<NPCPersonality>(File.ReadAllText(file))!;
   var agent=new NPCAgent(card,new HttpClient(),"unused","https://example.test/v1",new StorageManager(new SaveMemory()),new ModConfig(),new QuietMonitor());
   foreach(var (relationship,user) in new[]{("Married","今天好累，不想说话。"),("Married","你有什么自己想做的事？"),("Divorced","我们现在是什么关系，还住在一起吗？")})
   {
    var world=JsonSerializer.Serialize(new{relationship,friendshipHearts=relationship=="Married"?14:0,timeOfDay=2200,residence=relationship=="Married"?"Lives with player at farm":"Not confirmed",gameDate="Y4 winter 7 2200"});
    rows.Add(new{npc=card.Name,relationship,user,system=agent.BuildSystemPrompt(world,user)});
   }
  }
  File.WriteAllText(path,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
 }
 public static void Export(string path)
 {
  var character=JsonSerializer.Deserialize<NPCPersonality>(File.ReadAllText("assets/personalities/abigail.json"))!;
  var agent=new NPCAgent(character,new HttpClient(),"unused","https://example.test/v1",new StorageManager(new SaveMemory()),new ModConfig(),new QuietMonitor());
  var world="{\"gameDate\":\"Y4 winter 7 2210\",\"relationship\":\"Married\",\"residence\":\"Lives with the player at the farm\",\"farmName\":\"荔枝\",\"npcLocation\":\"FarmHouse\",\"location\":\"FarmHouse\",\"playerName\":\"爱吃桂味\",\"weddingDate\":\"Year 4, fall 8, Monday\",\"timeOfDay\":2210,\"isRaining\":false,\"isSnowing\":false}";
  var queries=new[]{"我们现在是什么关系？平时一起住在哪里？婚礼是哪天？","今天好累，不想说话。","今天别去矿洞了，陪我待会儿。","你喜欢的那些冒险也太无聊了。","你有什么自己想做的事吗？","我最喜欢钓鱼，最喜欢紫水晶。","你还记得我们去年一起去沙漠旅行吗？","只告诉我我们的婚礼日期，不用说其他的。"};
  File.WriteAllText(path,JsonSerializer.Serialize(queries.Select(q=>new{user=q,system=agent.BuildSystemPrompt(world,q)}),new JsonSerializerOptions{WriteIndented=true}));
 }
 private class QuietMonitor:StardewModdingAPI.IMonitor {public void Log(string t,StardewModdingAPI.LogLevel l){}}
}
