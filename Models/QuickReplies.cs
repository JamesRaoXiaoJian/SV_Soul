using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace SV_SOUL.Models;
public sealed record QuickReply(string Id,string Text);
public static class QuickReplies
{
    public static IReadOnlyList<QuickReply> Create(NPCPersonality character,string world,bool chinese,bool hasReply,string lastReply="")
    {
        using var doc=JsonDocument.Parse(RelationshipPolicy.Normalize(world,character));
        var state=doc.RootElement;
        string Get(string key)=>state.TryGetProperty(key,out var v)?v.ToString():"";
        bool Flag(string key)=>state.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
        var choices=new List<QuickReply>();
        void Add(string id,string zh,string en)=>choices.Add(new(id,chinese?zh:en));
        Add("day","今天过得怎么样？","How has your day been?");
        Add("plans","最近有什么想做的事？","What would you like to do lately?");
        if(Get("relationshipMode")=="Child")Add("play","最近喜欢玩什么？","What do you like playing lately?");
        else Add("interest","最近有什么让你感兴趣的事？","What has caught your interest lately?");
        Add("quiet","我们安静待一会儿吧。","Let's enjoy a quiet moment.");
        Add("care","希望你今天过得轻松些。","I hope today is a little easier for you.");
        if(Flag("isBirthday"))Add("birthday","生日快乐！","Happy birthday!");
        if(Flag("isFestival"))Add("festival","今天的节日有什么打算？","Any plans for today's festival?");
        if(Flag("isRaining"))Add("rain","你喜欢这样的雨天吗？","Do you like rainy days like this?");
        if(Flag("isSnowing"))Add("snow","这样的雪天让你想到什么？","What does this snowy day remind you of?");
        if(Get("relationshipMode")=="Roommate" && Get("relationship")=="Roommate")Add("roommate","一起生活有什么想调整的吗？","Anything you'd change about living together?");
        else if(Get("relationshipMode")=="Romance" && Get("relationship") is "Dating" or "Engaged" or "Married")
            Add("affection","我很珍惜和你相处的时间。","I cherish our time together.");
        else Add("perspective","聊聊你平时在意的事吧。","Tell me about what matters to you.");
        if(hasReply)
        {
            if(new[]{"愿意","要不要","一起","would you like","want to"}.Any(c=>lastReply.Contains(c,StringComparison.OrdinalIgnoreCase)))
            {
                Add("agree","好呀，我愿意。","Yes, I'd like that.");
                Add("decline","这次先不了。","Not this time.");
            }
            Add("more","能再多说一点吗？","Could you tell me more?");
            Add("why","你为什么这么想？","What makes you think that?");
            Add("different","我有不同的看法。","I see it differently.");
            Add("thanks","谢谢你愿意告诉我。","Thank you for sharing that with me.");
        }
        return choices;
    }
}
public sealed class QuickReplyPager
{
    private IReadOnlyList<QuickReply> items=Array.Empty<QuickReply>();
    public int PageSize { get; private set; }=4;
    public int Page { get; private set; }
    public int Selected { get; private set; }
    public int PageCount=>Math.Max(1,(items.Count+PageSize-1)/PageSize);
    public IReadOnlyList<QuickReply> Visible=>items.Skip(Page*PageSize).Take(PageSize).ToArray();
    public QuickReply? Current=>Visible.Count>Selected?Visible[Selected]:null;
    public void SetItems(IReadOnlyList<QuickReply> values) { items=values;Page=0;Selected=0; }
    public void SetPageSize(int value) { PageSize=value<=2?2:4;Page=Math.Min(Page,PageCount-1);Selected=Math.Min(Selected,Math.Max(0,Visible.Count-1)); }
    public void NextPage(){Page=(Page+1)%PageCount;Selected=0;}
    public void Select(int index){if(index>=0 && index<Visible.Count)Selected=index;}
    public void Move(int dx,int dy)
    {
        int column=Math.Clamp(Selected%2+dx,0,1),row=Math.Clamp(Selected/2+dy,0,PageSize/2-1);
        Select(row*2+column);
    }
}
