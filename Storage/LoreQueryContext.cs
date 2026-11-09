using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace SV_SOUL.Storage;
/// <summary>Immutable request-local knowledge permissions; never retained in the shared index.</summary>
public sealed record LoreQueryContext(string Npc, IReadOnlySet<string> Conditions, string RecentText = "",string CurrentFestival = "",string CurrentLocation = "")
{
    public static LoreQueryContext FromSnapshot(string npc,string world,string recent="")
    {
        var allowed=new HashSet<string>(StringComparer.Ordinal);
        using var json=JsonDocument.Parse(world);
        var root=json.RootElement;
        if(root.TryGetProperty("relationship",out var relationship))allowed.Add("relationship:"+relationship.GetString());
        if(root.TryGetProperty("friendshipHearts",out var hearts)&&hearts.TryGetInt32(out var count))
            for(int i=0;i<=Math.Min(count,14);i++)allowed.Add("hearts:"+i);
        foreach(var key in new[]{"seenEvents","receivedMail","allowedGameQueries"})
            if(root.TryGetProperty(key,out var list)&&list.ValueKind==JsonValueKind.Array)
                foreach(var value in list.EnumerateArray())allowed.Add((key=="seenEvents"?"event:":key=="receivedMail"?"mail:":"gamequery:")+value.ToString());
        string Get(string key)=>root.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
        return new(npc,allowed,recent,Get("festivalName"),Get("npcLocation")==""?Get("location"):Get("npcLocation"));
    }
}
/// <summary>Matches aliases in query-length time without scanning all documents.</summary>
internal sealed class LoreAliasIndex
{
    private sealed class Node { public Dictionary<char,Node> Children=new(); public string? Alias; }
    private readonly Node root=new();
    public LoreAliasIndex(IEnumerable<string> aliases)
    {
        foreach(var alias in aliases.Where(a=>a.Length>1 || (a.Length==1 && a[0]>127)).Distinct(StringComparer.OrdinalIgnoreCase))
        {var node=root;foreach(var c in alias.ToLowerInvariant()){if(!node.Children.TryGetValue(c,out var child))node.Children[c]=child=new Node();node=child;}node.Alias=alias.ToLowerInvariant();}
    }
    private IEnumerable<(int Start,string Alias)> Matches(string text)
    {
        text=text.ToLowerInvariant();
        static bool Word(char c)=>c<128 && (char.IsLetterOrDigit(c)||c=='_');
        for(int start=0;start<text.Length;start++)
        {
            var node=root;
            for(int end=start;end<text.Length && node.Children.TryGetValue(text[end],out node);end++)
            {
                if(node.Alias==null)continue;
                if(Word(text[start]) && start>0 && Word(text[start-1]))continue;
                if(Word(text[end]) && end+1<text.Length && Word(text[end+1]))continue;
                yield return (start,node.Alias);
            }
        }
    }
    private IEnumerable<(int Start,string Alias)> BestMatches(string text)
    {
        var matches=Matches(text).ToArray();
        return matches.Where(m=>!matches.Any(other=>other.Alias.Length>m.Alias.Length && other.Start<=m.Start && other.Start+other.Alias.Length>=m.Start+m.Alias.Length));
    }
    public HashSet<string> Match(string text)=>BestMatches(text).Select(m=>m.Alias).ToHashSet(StringComparer.Ordinal);
    public string? LastMatch(string text)=>BestMatches(text).OrderByDescending(m=>m.Start).ThenByDescending(m=>m.Alias.Length).Select(m=>m.Alias).FirstOrDefault();
}
