using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace SV_SOUL.Storage;
/// <summary>Main-thread coordinator; workers receive detached data only. Never waits for a build.</summary>
public sealed class LoreIndexHost : ILoreRetriever, IDisposable
{
    private readonly object gate=new();
    private readonly Func<IReadOnlyList<LoreEntry>,LoreRetriever> build;
    private Task<LoreRetriever>? pending;
    private LoreRetriever? current;
    private long generation;
    private long pendingGeneration;
    private bool disposed;
    public int EntryCount { get { lock(gate)return current?.Count ?? 0; } }
    public bool IsBuilding { get { lock(gate)return pending!=null; } }
    public bool IsReady { get { lock(gate)return current!=null; } }
    public LoreIndexHost(Func<IReadOnlyList<LoreEntry>,LoreRetriever>? builder=null) => build=builder ?? (entries=>new LoreRetriever(entries));
    public void Invalidate()
    {
        lock(gate){generation++;current?.Dispose();current=null;}
    }
    public bool Start(IReadOnlyList<LoreEntry> entries,string gameVersion="")
    {
        lock(gate)
        {
            if(disposed || pending!=null)return false;
            // Freeze caller-owned mutable DTOs before the worker can access them.
            var snapshot=System.Text.Json.JsonSerializer.Deserialize<List<LoreEntry>>(System.Text.Json.JsonSerializer.Serialize(entries.Where(e=>LoreRetriever.Compatible(e.GameVersion,gameVersion)).ToList()))!;
            pendingGeneration=generation;
            pending=Task.Run(()=>build(snapshot));
            return true;
        }
    }
    public bool Poll(out Exception? error)
    {
        lock(gate)
        {
            error=null;
            if(pending==null || !pending.IsCompleted)return false;
            var completed=pending;pending=null;
            if(completed.IsFaulted){error=completed.Exception?.GetBaseException();return false;}
            if(completed.IsCanceled)return false;
            var result=completed.Result;
            if(disposed || pendingGeneration!=generation){result.Dispose();return false;}
            current?.Dispose();current=result;return true;
        }
    }
    public string Recall(string query,string npc)=>Recall(query,new LoreQueryContext(npc,new HashSet<string>()));
    public string Recall(string query,LoreQueryContext context)
    {lock(gate)return current?.Recall(query,context) ?? "";}
    public LoreRecall Retrieve(string query,LoreQueryContext context)
    {lock(gate)return current?.Retrieve(query,context) ?? LoreRecall.Empty;}
    public void Dispose()
    {
        lock(gate)
        {
            if(disposed)return;disposed=true;generation++;current?.Dispose();current=null;
            var abandoned=pending;pending=null;
            if(abandoned!=null)_=abandoned.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();else _=t.Exception;},TaskScheduler.Default);
        }
    }
}
