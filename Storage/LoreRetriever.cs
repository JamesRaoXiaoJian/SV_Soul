using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lucene.Net.Analysis.Cjk;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;
namespace SV_SOUL.Storage;
public interface ILoreRetriever
{
    string Recall(string query,string npc);
    string Recall(string query,LoreQueryContext context);
    LoreRecall Retrieve(string query,LoreQueryContext context);
}
public sealed record LoreRecall(IReadOnlyList<LoreHit> Hits,bool HasWithheldKnowledge)
{
    public static LoreRecall Empty { get; } = new(Array.Empty<LoreHit>(),false);
    public string Text => string.Join("\n",Hits.Select(h=>"["+h.Id+"] "+h.Text));
}
public sealed record LoreHit(string Id,string Text,string Source,float Score);
/// <summary>Shared immutable index. Permissions and discourse references belong to each request.</summary>
public sealed class LoreRetriever : ILoreRetriever, IDisposable
{
    private readonly object gate=new();
    private bool disposed;
    private readonly RAMDirectory directory=new();
    private readonly CJKAnalyzer analyzer=new(LuceneVersion.LUCENE_48);
    private readonly DirectoryReader reader;
    private readonly IndexSearcher searcher;
    private readonly Dictionary<string,LoreEntry> entries;
    private readonly LoreAliasIndex aliases;
    private readonly Dictionary<string,string[]> entitiesByAlias;
    private readonly Dictionary<string,LoreEntry> rules;
    public int Count => entries.Count;
    public LoreRetriever(IEnumerable<LoreEntry> input,string gameVersion="")
    {
        var valid=input.Where(e=>!string.IsNullOrWhiteSpace(e.Id)&&!string.IsNullOrWhiteSpace(e.Text)&&!string.IsNullOrWhiteSpace(e.Source)&&Compatible(e.GameVersion,gameVersion));
        // Effective local game assets win over curated versions of the same fact.
        entries=valid.GroupBy(e=>string.IsNullOrEmpty(e.FactKey)?e.Id:e.FactKey)
            .Select(g=>g.OrderByDescending(e=>e.Source.StartsWith("game:",StringComparison.Ordinal)).First())
            .GroupBy(e=>e.Id).ToDictionary(g=>g.Key,g=>g.First());
        aliases=new LoreAliasIndex(entries.Values.SelectMany(e=>e.Aliases));
        entitiesByAlias=entries.Values.SelectMany(e=>e.Aliases.Select(a=>(Alias:a.ToLowerInvariant(),Entities:e.EntityIds)))
            .GroupBy(x=>x.Alias).ToDictionary(g=>g.Key,g=>g.SelectMany(x=>x.Entities).Distinct().ToArray());
        rules=entries.Values.GroupBy(RuleKey).ToDictionary(g=>g.Key,g=>g.First());
        using(var writer=new IndexWriter(directory,new IndexWriterConfig(LuceneVersion.LUCENE_48,analyzer){Similarity=new BM25Similarity()}))
        {
            foreach(var e in entries.Values)
            {
                var doc=new Document {new StringField("id",e.Id,Field.Store.YES),new TextField("text",e.Text+" "+string.Join(" ",e.Aliases)+" "+string.Join(" ",e.Topics),Field.Store.NO),new StringField("rule",RuleKey(e),Field.Store.NO),new StringField("kind",e.Kind,Field.Store.NO)};
                foreach(var owner in e.KnownBy.Length==0?new[]{"*"}:e.KnownBy)doc.Add(new StringField("owner",owner,Field.Store.NO));
                foreach(var entity in e.EntityIds)doc.Add(new StringField("entity",entity,Field.Store.NO));
                foreach(var alias in e.Aliases)doc.Add(new StringField("alias",alias.ToLowerInvariant(),Field.Store.NO));
                writer.AddDocument(doc);
            }
            writer.Commit();
        }
        reader=DirectoryReader.Open(directory);
        searcher=new IndexSearcher(reader){Similarity=new BM25Similarity()};
    }
    public static bool Compatible(string source,string current)
    {
        if(source==""||current=="")return true;
        return Version.TryParse(source,out var a)&&Version.TryParse(current,out var b)&&a.Major==b.Major&&a.Minor==b.Minor;
    }
    private static string RuleKey(LoreEntry e)=>JsonSerializer.Serialize(new{e.Condition,e.RequiredAll,e.RequiredAny});
    private static bool Permits(LoreEntry e,LoreQueryContext context)
    {
        bool Has(string c)=>c.Length>0 && (c.StartsWith("gamequery:",StringComparison.Ordinal)||c.StartsWith("event:",StringComparison.Ordinal)||c.StartsWith("mail:",StringComparison.Ordinal)||c.StartsWith("hearts:",StringComparison.Ordinal)||c.StartsWith("relationship:",StringComparison.Ordinal)) && context.Conditions.Contains(c);
        return (e.Condition==""||Has(e.Condition)) && e.RequiredAll.All(Has) && (e.RequiredAny.Length==0||e.RequiredAny.Any(Has));
    }
    // Nest term disjunctions instead of silently dropping candidates at Lucene's clause limit.
    private static Query Any(string field,IEnumerable<string> values)
    {
        var clauses=values.Distinct().Select(v=>(Query)new TermQuery(new Term(field,v))).ToList();
        while(clauses.Count>512)clauses=clauses.Chunk(512).Select(group=>{var q=new BooleanQuery();foreach(var c in group)q.Add(c,Occur.SHOULD);return (Query)q;}).ToList();
        var result=new BooleanQuery();foreach(var c in clauses)result.Add(c,Occur.SHOULD);return result;
    }
    private static string TopicKind(string text)
    {
        if(new[]{"生日","哪天出生","birthday","born"}.Any(t=>text.Contains(t,StringComparison.OrdinalIgnoreCase)))return "birthday";
        if(new[]{"家人","家庭","父母","妈妈","爸爸","母亲","父亲","兄弟","姐妹","family","parents","mother","father","sister","brother"}.Any(t=>text.Contains(t,StringComparison.OrdinalIgnoreCase)))return "family";
        if(new[]{"什么时候","几点","哪天举行","几号举行","日程","when","schedule"}.Any(t=>text.Contains(t,StringComparison.OrdinalIgnoreCase)))return "calendar";
        if(new[]{"你是谁","你的工作","做什么工作","介绍自己","介绍一下你自己","你自己","职业","who are you","your job","what do you do"}.Any(t=>text.Contains(t,StringComparison.OrdinalIgnoreCase)))return "character";
        return "";
    }
    public string Recall(string query,string npc)=>Recall(query,new LoreQueryContext(npc,new HashSet<string>()));
    public string Recall(string query,LoreQueryContext context)=>Retrieve(query,context).Text;
    public IReadOnlyList<LoreHit> Search(string query,LoreQueryContext context)=>Retrieve(query,context).Hits;
    public LoreRecall Retrieve(string query,LoreQueryContext context)
    {
        lock(gate)
        {
            if(disposed||string.IsNullOrWhiteSpace(query))return LoreRecall.Empty;
            query=query.Length>1000?query[..1000]:query;
            var matched=aliases.Match(query);
            var kind=TopicKind(query);
            string? subject=null;
            if(matched.Count==0 && Regex.IsMatch(query,@"(^|[，。？！\s])(她|他|它)|她的|他的|那个|那里|那位|\b(there|her|his|that place|that person)\b",RegexOptions.IgnoreCase))
            {
                var previous=aliases.LastMatch(context.RecentText);
                if(previous!=null){matched.Add(previous);query+=" "+previous;}
            }
            if(matched.Count==0 && Regex.IsMatch(query,@"今天.*(节日|庆典|活动)|today.*festival",RegexOptions.IgnoreCase))
            {
                matched=aliases.Match(context.CurrentFestival);
                if(matched.Count>0)query+=" "+string.Join(" ",matched);
            }
            if(matched.Count==0 && Regex.IsMatch(query,@"这里|这儿|\bhere\b",RegexOptions.IgnoreCase))
            {
                matched=aliases.Match(context.CurrentLocation);
                if(matched.Count>0)query+=" "+string.Join(" ",matched);
            }
            if(matched.Count==0 && kind!="" && !Regex.IsMatch(query,@"^(我|my\b|our\b)",RegexOptions.IgnoreCase))
            {subject=context.Npc;query+=" "+context.Npc;}
            if(matched.Count==0 && subject==null)return LoreRecall.Empty;
            var terms=new BooleanQuery();
            using(var stream=analyzer.GetTokenStream("text",new StringReader(query)))
            {var term=stream.AddAttribute<ICharTermAttribute>();stream.Reset();int count=0;while(stream.IncrementToken() && count++<128)terms.Add(new TermQuery(new Term("text",term.ToString())),Occur.SHOULD);stream.End();}
            var entities=matched.SelectMany(a=>entitiesByAlias.GetValueOrDefault(a,Array.Empty<string>())).Distinct().ToArray();
            Query anchor=subject!=null?Any("entity",new[]{subject}):entities.Length>0?new BooleanQuery{{Any("entity",entities),Occur.SHOULD},{Any("alias",matched),Occur.SHOULD}}:Any("alias",matched);
            var common=new BooleanQuery{{terms,Occur.MUST},{anchor,Occur.MUST},{Any("owner",context.Npc is "Bear" or "???" ? new[]{context.Npc} : new[]{"*",context.Npc}),Occur.MUST}};
            if(kind!="")common.Add(Any("kind",new[]{kind}),Occur.MUST);
            var permitted=rules.Where(r=>Permits(r.Value,context)).Select(r=>r.Key).ToHashSet();
            var combined=new BooleanQuery{{common,Occur.MUST},{Any("rule",permitted),Occur.MUST}};
            var hidden=new BooleanQuery{{common,Occur.MUST},{Any("owner",new[]{context.Npc}),Occur.MUST},{Any("rule",rules.Keys.Where(k=>!permitted.Contains(k))),Occur.MUST}};
            var withheld=searcher.Search(hidden,1).TotalHits>0;
            var hits=new List<LoreHit>();int size=0;
            foreach(var hit in searcher.Search(combined,12).ScoreDocs)
            {
                var e=entries[searcher.Doc(hit.Doc).Get("id")];var length=e.Id.Length+e.Text.Length+3;
                if(size+length+(hits.Count>0?1:0)>1000)continue;
                hits.Add(new(e.Id,e.Text,e.Source,hit.Score));size+=length+(hits.Count>1?1:0);
                if(hits.Count==3)break;
            }
            return new LoreRecall(hits,withheld);
        }
    }
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;reader.Dispose();analyzer.Dispose();directory.Dispose();}}
}
