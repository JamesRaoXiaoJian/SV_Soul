using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis.Cjk;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;
namespace SV_SOUL.Storage;
public interface IMemoryRetriever { IReadOnlyList<MemoryRecord> Recall(IEnumerable<MemoryRecord> records, string query, int limit = 8, string world = ""); }
public sealed class MemoryRetriever : IMemoryRetriever, IDisposable
{
    private RAMDirectory? directory;
    private DirectoryReader? reader;
    private readonly CJKAnalyzer analyzer = new(LuceneVersion.LUCENE_48);
    private string signature = "";
    private static long GameOrder(MemoryRecord record)
    {
        var parts=record.GameTime.Split(' ');
        if(parts.Length<3||!int.TryParse(parts[0].TrimStart('Y'),out var year)||!int.TryParse(parts[2],out var day))return -1;
        var season=Array.IndexOf(new[]{"spring","summer","fall","winter"},parts[1].ToLowerInvariant());
        if(season<0)return -1;
        int time=0;if(parts.Length>3)int.TryParse(parts[3],out time);
        return ((year-1L)*112+season*28+day)*10000+time;
    }
    public IReadOnlyList<MemoryRecord> Recall(IEnumerable<MemoryRecord> records, string query, int limit = 8, string world = "")
    {
        bool historical=new[]{"以前","曾经","过去","当时","结婚前","previous","before","used to"}.Any(w=>query.Contains(w,StringComparison.OrdinalIgnoreCase));
        var active = records.Where(r => r.Active || historical).ToList();
        int Day(string text)
        {
            var parts=text.Split(' '); if(parts.Length<3||!int.TryParse(parts[0].TrimStart('Y'),out var year)||!int.TryParse(parts[2],out var day))return -1;
            var season=Array.IndexOf(new[]{"spring","summer","fall","winter"},parts[1].ToLowerInvariant());return season<0?-1:(year-1)*112+season*28+day;
        }
        if(!string.IsNullOrEmpty(world))
        {
            using var doc=System.Text.Json.JsonDocument.Parse(world);
            if(doc.RootElement.TryGetProperty("gameDate",out var date))
            {
                int now=Day(date.ToString());int offset=query.Contains("前天")?2:query.Contains("昨天")||query.Contains("yesterday",StringComparison.OrdinalIgnoreCase)?1:query.Contains("今天")||query.Contains("today",StringComparison.OrdinalIgnoreCase)?0:-1;
                if(now>=0&&offset>=0)active=active.Where(r=>Day(r.GameTime)==now-offset).ToList();
            }
        }
        string SearchText(string text)
        {
            foreach(var pair in new[]{("fishing","钓鱼 垂钓"),("amethyst","紫水晶"),("pumpkin","南瓜"),("relationship","关系"),("Married","结婚 妻子 丈夫"),("Divorced","离婚"),("residence","住所 住 哪里"),("FarmHouse","农场 家")})text=text.Replace(pair.Item1,pair.Item2,StringComparison.OrdinalIgnoreCase);
            return text;
        }
        var next = string.Join("|", active.Select(r => r.Id));
        if (next != signature || reader == null)
        {
            reader?.Dispose(); directory?.Dispose(); directory = new RAMDirectory();
            using (var writer = new IndexWriter(directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer) { Similarity = new BM25Similarity() }))
            {
                foreach (var r in active)
                    writer.AddDocument(new Document { new StringField("id", r.Id, Field.Store.YES), new TextField("text", SearchText(r.Text + " " + r.Key), Field.Store.NO) });
                writer.Commit();
            }
            reader = DirectoryReader.Open(directory); signature = next;
        }
        var q = new BooleanQuery();
        using (var stream = analyzer.GetTokenStream("text", new System.IO.StringReader(SearchText(query))))
        {
            var term = stream.AddAttribute<ICharTermAttribute>(); stream.Reset(); int count=0;
            while (stream.IncrementToken() && count++ < 64) q.Add(new TermQuery(new Term("text", term.ToString())), Occur.SHOULD);
            stream.End();
        }
        var searcher = new IndexSearcher(reader) { Similarity = new BM25Similarity() };
        var hits = searcher.Search(q, 32).ScoreDocs.Select(d => searcher.Doc(d.Doc).Get("id")).ToList();
        var recent=active.OrderByDescending(GameOrder).ThenByDescending(r=>r.RecordedAt).Take(20).Select(r=>r.Id).ToList();
        var candidates=active.Where(r=>hits.Contains(r.Id)||recent.Contains(r.Id)).ToList();
        var important=candidates.OrderByDescending(r=>r.Importance).ThenByDescending(GameOrder).ThenByDescending(r=>r.RecordedAt).Select(r=>r.Id).ToList();
        double Rank(List<string> list,string id,double weight) { var rank=list.IndexOf(id); return rank<0?0:weight/(60+rank+1); }
        return candidates.OrderByDescending(r=>Rank(hits,r.Id,2)+Rank(recent,r.Id,0.5)+Rank(important,r.Id,0.25)).Take(limit).ToList();
    }

    public void Dispose() { reader?.Dispose(); directory?.Dispose(); analyzer.Dispose(); }
}
