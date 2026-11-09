using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace SV_SOUL.Models;
public class DialogueExample
{
    public string Relationship { get; set; } = "";
    public string Topic { get; set; } = "";
    public string[] Cues { get; set; } = Array.Empty<string>();
    public string User { get; set; } = "";
    public string Reply { get; set; } = "";
}
public static class DialogueVoice
{
    public static string Scene(JsonElement world)
    {
        string Get(string key) => world.TryGetProperty(key,out var v)?v.ToString():"unknown";
        var lines=new List<string>();
        void Add(string key,string label) { var value=Get(key); if(value!="unknown" && value!="")lines.Add(label+value); }
        Add("gameDate","当前游戏日期和时间：");
        var relation=Get("relationship");
        lines.Add(relation switch {"Married"=>"你与当前玩家已经结婚，但婚姻身份不替代当前的相处质量；仍有各自的兴趣和判断。", "Dating"=>"你与玩家正在交往，尚未结婚。", "Engaged"=>"你与玩家已订婚，婚礼尚未发生。", "Divorced"=>"你与玩家已经离婚，旧婚姻属于过去。", "Roommate"=>"你与玩家是同住室友，不是夫妻。", "NoFriendshipRecord" or "NotMetOrNoRecordedFriendship"=>"游戏没有这段关系的好感记录；是否认识以真实对话与经历为准，不默认初次见面。", _=>"当前关系记录："+relation});
        if(world.TryGetProperty("friendshipHearts",out var hearts)&&hearts.TryGetInt32(out var n))
            lines.Add(n>=8?"当前好感较高，可以更熟悉、放松；具体信任与共同经历仍以记忆为准。":n<=2?"当前好感较低，保留适当距离；不要否认已确认的婚姻或过去经历，也不要假装关系亲密。":"你与玩家已有一定熟悉，但亲密表达仍应符合当前关系。");
        Add("playerName","玩家的名字：");Add("residence","当前住处（不是正在访问的地点）：");Add("farmName","农场名字：");
        Add("npcLocation","你现在所在地点：");Add("location","玩家所在地点：");Add("weddingDate","已确认的婚礼日期：");Add("cohabitationDate","已确认的同住开始日期：");
        Add("children","家庭中孩子的名字：");Add("isBirthday","今天是否你的生日：");Add("festivalName","今天的公共节日：");Add("festivalLocationAndTime","节日举办条件：");
        if(Get("isRaining").Equals("True",StringComparison.OrdinalIgnoreCase))lines.Add("你所在地区正在下雨。");
        if(Get("isSnowing").Equals("True",StringComparison.OrdinalIgnoreCase))lines.Add("现在正在下雪。");
        return string.Join("\n",lines);
    }
    public static IReadOnlyList<string> Situations(JsonElement world)
    {
        var keys=new List<string>();
        bool True(string key)=>world.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
        if(True("isBirthday"))keys.Add("birthday");
        if(True("isFestival"))keys.Add("festival");
        if(True("isRaining"))keys.Add("raining");
        if(True("isSnowing"))keys.Add("snowing");
        if(world.TryGetProperty("timeOfDay",out var time)&&time.TryGetInt32(out var hour))keys.Add(hour>=2000?"night":hour<1200?"morning":"day");
        if(world.TryGetProperty("friendshipHearts",out var hearts)&&hearts.TryGetInt32(out var n))keys.Add(n>=8?"close":n<=2?"distant":"familiar");
        if(world.TryGetProperty("giftsToday",out var gifts)&&gifts.TryGetInt32(out var count)&&count>0)keys.Add("gift");
        return keys;
    }
    public static string Situation(NPCPersonality personality,JsonElement world)
    {
        return string.Join("\n",Situations(world).Where(personality.SituationHints.ContainsKey).Take(3).Select(k=>personality.SituationHints[k]));
    }
    public static string Direction => "你是在和眼前的人相处，不是在撰写人物资料。即使被问身份、住处或日期，也用角色自然说话的方式把答案融进台词；避免逐项报数或换段列出字段。除非玩家明确要求只报答案。回应这个人此刻的意思，而不只是复述资料。根据当前话语和此前对话，选择适合的反应：直接答复、认真倾听、调侃、分享看法、表达分歧或安静结束。没有一种反应每次都适合。保留你自己的好恶和愿望；亲近不等于无条件赞同。允许形成当下的主观感受和有条件的提议，但不要把愿望写成已经完成的游戏动作。保持前几轮的情绪连续，必要时自然转变。不替玩家决定感受或行动。不输出分析、角色卡、字段或舞台说明；直接说台词。不要每句加昵称、反问、感叹号或标志性爱好；准确回答仍然重要，用户明确要求简短直答时照做。";
    public static string Examples(NPCPersonality personality,string query,string relationship="")
    {
        var selected=personality.DialogueExamples.Where(e=>string.IsNullOrEmpty(e.Relationship)||e.Relationship==relationship).Select((e,i)=>new{Example=e,Order=i,Score=e.Cues.Count(c=>query.Contains(c,StringComparison.OrdinalIgnoreCase))})
            .OrderByDescending(x=>x.Score).ThenBy(x=>x.Example.Topic=="quiet"||x.Example.Topic=="opinion"?0:1).ThenBy(x=>x.Order).Take(2);
        return string.Join("\n",selected.Select(x=>$"示例玩家：{x.Example.User}\n示例角色：{x.Example.Reply}"));
    }
}
