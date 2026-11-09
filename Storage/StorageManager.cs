using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace SV_SOUL.Storage;
public class StorageManager : IMemoryRepository
{
    private readonly object gate = new();
    private SaveMemory state;
    public int ResetVersion { get; private set; }
    private readonly string? standaloneFile;
    private readonly Dictionary<string, MemoryRetriever> indexes = new();
    public StorageManager(SaveMemory snapshot) { state = Clone(snapshot); }
    // Standalone tests use their own isolated file; the game uses save-data exclusively.
    public StorageManager(string directory)
    {
        Directory.CreateDirectory(directory); standaloneFile = Path.Combine(directory,"memory-v2.json");
        state = File.Exists(standaloneFile) ? JsonSerializer.Deserialize<SaveMemory>(File.ReadAllText(standaloneFile))! : new SaveMemory();
    }
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private void Persist() { if(standaloneFile != null) { var tmp=standaloneFile+".tmp"; File.WriteAllText(tmp,JsonSerializer.Serialize(state)); File.Move(tmp,standaloneFile,true); } }
    public SaveMemory Export() { lock(gate) return Clone(state); }
    public List<ConversationTurn> LoadHistoryPage(string npc,int limit) { lock(gate) return state.Conversations.TryGetValue(npc,out var v) ? Clone(v.TakeLast(Math.Max(0,limit)).ToList()) : new(); }
    public List<ConversationTurn> LoadHistory(string npc) { lock(gate) return state.Conversations.TryGetValue(npc,out var v) ? Clone(v) : new(); }
    public void SaveHistory(string npc,List<ConversationTurn> turns) { lock(gate) { state.Conversations[npc]=Clone(turns); Persist(); } }
    public NPCMemory LoadMemories(string npc) { lock(gate) return state.Npcs.TryGetValue(npc,out var v) ? Clone(v) : new(); }
    public void SaveMemories(string npc,NPCMemory memory) { lock(gate) { memory.LastUpdated=DateTime.UtcNow; state.Npcs[npc]=Clone(memory); Persist(); } }
    public void MarkExtracted(string npc,int count)
    {
        lock(gate){var memory=LoadMemories(npc);memory.ExtractedThrough=Math.Max(memory.ExtractedThrough,count);SaveMemories(npc,memory);}
    }
    public void AddEvidence(string npc,MemoryRecord record)
    {
        lock(gate) { var memory=LoadMemories(npc); memory.Records.Add(record); SaveMemories(npc,memory); }
    }
    public void MergeStatements(string npc,IEnumerable<MemoryRecord> records)
    {
        lock(gate)
        {
            var memory=LoadMemories(npc);
            foreach(var record in records)
            {
                if(memory.Records.Any(r=>r.Active && r.Kind==record.Kind && r.Key==record.Key && r.Text==record.Text))continue;
                foreach(var old in memory.Records.Where(r=>r.Active&&r.Kind==record.Kind&&r.Key==record.Key)){old.Active=false;old.SupersededBy=record.Id;old.ValidUntil=record.GameTime;}
                memory.Records.Add(record);
            }
            SaveMemories(npc,memory);
        }
    }
    public void Observe(string npc,string gameTime,string world)
    {
        lock(gate)
        {
            var memory=LoadMemories(npc);
            using var doc=JsonDocument.Parse(world);
            // Only lifecycle facts become long-term observations; transient weather stays in the snapshot.
            var facts=doc.RootElement.EnumerateObject().Where(p=>new[]{"relationship","residence","spouse","friendshipHearts"}.Contains(p.Name));
            foreach(var p in facts)
            {
                var text=p.Name+": "+p.Value.ToString();
                var previous=memory.Records.LastOrDefault(r=>r.Active && r.Kind=="GameFact" && r.Key==p.Name);
                if(previous?.Text==text) continue;
                var record=new MemoryRecord { Kind="GameFact",Key=p.Name,Text=text,Importance=4,GameTime=gameTime,EvidenceIds=new(){"snapshot:"+Guid.NewGuid().ToString("N")} };
                if(previous!=null){previous.Active=false;previous.SupersededBy=record.Id;previous.ValidUntil=record.GameTime;}
                memory.Records.Add(record);
            }
            // A relationship reset by the game's memory-wipe mechanics must not preserve spouse memories.
            using var previousWorld=JsonDocument.Parse(string.IsNullOrEmpty(memory.LastWorldState)?"{}":memory.LastWorldState);
            if(previousWorld.RootElement.TryGetProperty("relationship",out var oldStatus) && oldStatus.ToString()=="Divorced"
                && doc.RootElement.TryGetProperty("relationship",out var newStatus) && newStatus.ToString()=="Friendly")
            {
                memory.Records.RemoveAll(r=>r.Kind!="GameFact" || !r.Active);
                state.Conversations.Remove(npc);
                ResetVersion++;
            }
            memory.LastWorldState=world;SaveMemories(npc,memory);
        }
    }
    public string GetMemoriesSummary(string npc,string query="")
    {
        lock(gate)
        {
            var memory=state.Npcs.TryGetValue(npc,out var existing)?existing:new NPCMemory();
            if(!indexes.TryGetValue(npc,out var index))indexes[npc]=index=new MemoryRetriever();
            var text=string.Join("\n",index.Recall(memory.Records,query,8,memory.LastWorldState).Select(r=>$"[{r.Kind}; observed game time={r.GameTime}; validUntil={r.ValidUntil ?? "current"}; id={r.Id}] {r.Text}"));
            return text.Length<=1800?text:text[..1800];
        }
    }
}
