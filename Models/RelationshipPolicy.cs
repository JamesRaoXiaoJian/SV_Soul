using System;
using System.Text.Json.Nodes;
namespace SV_SOUL.Models;
public static class RelationshipPolicy
{
    public static string Normalize(string json,NPCPersonality personality)
    {
        var world=JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        bool Flag(string key)=>world[key] is JsonValue v && v.TryGetValue<bool>(out var b)&&b;
        var relationship=world["relationship"]?.ToString()??"NotMetOrNoRecordedFriendship";
        var mode=personality.RelationshipMode;
        if(Flag("isChild")||mode=="Child") mode="Child";
        else if(relationship=="Roommate") mode="Roommate";
        else if(Flag("canBeRomanced")||(Flag("isPlayerSpouse")&&relationship=="Married")) mode="Romance";
        else if(world.ContainsKey("canBeRomanced")) mode=personality.Name=="Krobus"?"Roommate":"Social";
        else if(mode=="Auto") mode=relationship is "Dating" or "Engaged" or "Married"?"Romance":"Social";
        if((mode!="Romance" && relationship is "Dating" or "Engaged" or "Married") || (mode=="Child" && relationship=="Divorced"))
        {
            world["relationship"]="Friendly";world["isMarried"]=false;world["isDating"]=false;
            world.Remove("weddingDate");world.Remove("children");world["residence"]="unknown";
        }
        if(mode=="Roommate")
        {
            if(relationship=="Roommate" && world["weddingDate"]!=null)world["cohabitationDate"]=world["weddingDate"]!.ToString();
            world.Remove("weddingDate");
        }
        world["relationshipMode"]=mode;
        return world.ToJsonString();
    }
    public static string Guidance(string mode)=>mode switch
    {
        "Romance"=>"可以发展恋爱，但只有当前状态已确认时才使用恋人或配偶称呼。高好感本身不是恋爱。",
        "Roommate"=>"可以建立友谊，实际关系为 Roommate 时才表示已经同住。室友不是恋爱或婚姻；保留非浪漫的相处方式。",
        "Child"=>"你是孩子类角色。只进行适龄的日常、友谊或家庭交流，不进入恋爱、订婚或配偶角色。",
        _=>"这是非恋爱社交角色。友好、高好感和赠礼不转化为恋爱；保持自己的家庭、职业和相处边界。自然回应，不提游戏机制或攻略资格。"
    };
}
