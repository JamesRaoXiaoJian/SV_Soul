using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using StardewModdingAPI;
using StardewValley;
using SV_SOUL.Config;
using SV_SOUL.Models;
using SV_SOUL.Storage;
using SV_SOUL.UI;

namespace SV_SOUL.Agents;

public class AgentManager
{
    private readonly Dictionary<string, NPCAgent> _agents = new();
    private readonly Dictionary<string, NPCPersonality> _personalities = new();
    private readonly HttpClient _http = new();
    private StorageManager _storage;
    private int _generation;
    private CancellationTokenSource _sessionCancellation=new();
    private readonly ModConfig _config;
    private readonly LoreIndexHost _lore = new();
    private bool _loreDirty = true;
    private string _loreLocale="";
    public void InvalidateLore() { _loreDirty=true; _lore.Invalidate(); }
    private void RefreshLore()
    {
        var locale=_helper.GameContent.CurrentLocale;
        if(locale!=_loreLocale){_loreLocale=locale;InvalidateLore();}
        if(_lore.Poll(out var buildError))_monitor.Log($"Background index ready: {_lore.EntryCount} entries ({locale}).",LogLevel.Debug);
        if(buildError!=null)_monitor.Log("Background index unavailable: "+buildError.GetType().Name,LogLevel.Warn);
        if (!_loreDirty || _lore.IsBuilding) return;
        _loreDirty=false;
        try
        {
            var entries=GameLoreProvider.Capture(_helper, _personalities);
            var directory=Path.Combine(_helper.DirectoryPath,"assets","lore");
            if(Directory.Exists(directory))
                foreach(var path in Directory.EnumerateFiles(directory,"*.json").OrderBy(p=>p,StringComparer.Ordinal))
                    entries.AddRange(System.Text.Json.JsonSerializer.Deserialize<List<LoreEntry>>(File.ReadAllText(path)) ?? new());
            _lore.Start(entries,Game1.version);
        }
        catch(Exception ex){_monitor.Log("Background sources unavailable: "+ex.GetType().Name,LogLevel.Warn);}
    }
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;

    public AgentManager(IModHelper helper, IMonitor monitor, ModConfig config)
    {
        _helper = helper;
        _monitor = monitor;
        _config = config;
        _storage = new StorageManager(new SaveMemory());

        LoadPersonalities();
    }

    private void LoadPersonalities()
    {
        var directory=Path.Combine(_helper.DirectoryPath,"assets","personalities");
        foreach(var file in Directory.EnumerateFiles(directory,"*.json"))
        {
            try
            {
                var personality=System.Text.Json.JsonSerializer.Deserialize<NPCPersonality>(File.ReadAllText(file));
                if(personality==null || !IsEnabled(personality.Name))continue;
                _personalities[personality.Name]=personality;
            }
            catch(Exception ex){_monitor.Log($"Invalid character profile {Path.GetFileName(file)}: {ex.Message}",LogLevel.Warn);}
        }
        _monitor.Log($"Loaded {_personalities.Count} NPC profiles.",LogLevel.Info);
    }
    private bool IsEnabled(string name)=>!_config.ExcludedNPCs.Contains(name,StringComparer.OrdinalIgnoreCase) && (_config.EnabledNPCs.Contains("*")||_config.EnabledNPCs.Contains(name,StringComparer.OrdinalIgnoreCase));
    public bool IsConfiguredNPC(string npcName)
    {
        if(!IsEnabled(npcName))return false;
        if(npcName=="Dwarf" && Context.IsWorldReady && !Game1.player.canUnderstandDwarves)return false;
        if(_personalities.ContainsKey(npcName))return true;
        var npc=Game1.getCharacterFromName(npcName);
        if(npc==null || npc is StardewValley.Monsters.Monster || npc is StardewValley.Characters.Pet || npc is StardewValley.Characters.Horse || npc is StardewValley.Characters.Child)return false;
        _personalities[npcName]=new NPCPersonality
        {
            Name=npcName,DisplayName=npc.displayName,
            RelationshipMode=npc.Age==2?"Child":npc.datable.Value?"Romance":"Social",
            PreserveVanillaInteraction=true,
            CoreTraits="You are the encountered Stardew Valley NPC named "+npc.displayName+". No curated biography is available. Use only observed game context and established conversations; do not invent family, career or shared history.",
            Voice="自然、简短地交流。对于未提供的个人资料不要编造；承认不了解时不要提程序或角色档案。",
            Greetings=new[]{"你好，有什么想说的？"}
        };
        return true;
    }
    public bool PreserveVanilla(string npcName)=>_personalities.TryGetValue(npcName,out var profile)&&profile.PreserveVanillaInteraction;

    public void OpenChat(string npcName, Action<string, string> onResponse, bool controllerInput = false)
    {
        if (!Context.IsMainPlayer) { _monitor.Log("NPC memory currently supports the host player only.",LogLevel.Warn); return; }
        if (!_personalities.TryGetValue(npcName, out var personality))
        {
            _monitor.Log($"No personality configured for {npcName}", LogLevel.Warn);
            return;
        }

        var resetVersion=_storage.ResetVersion;
        var openingState=GameStateProvider.Capture(npcName).Json;
        using(var opening=System.Text.Json.JsonDocument.Parse(openingState))
            _storage.Observe(npcName,opening.RootElement.TryGetProperty("gameDate",out var date)?date.ToString():"unknown",openingState);
        ResetIfNeeded(resetVersion);
        if (!_agents.TryGetValue(npcName, out var agent))
        {
            _monitor.Log($"Creating agent for {npcName} (model={_config.Model}, baseUrl={_config.BaseUrl})", LogLevel.Info);
            agent = new NPCAgent(personality, _http, _config.ApiKey, _config.BaseUrl ?? (string.Equals(_config.ApiProvider, "OpenAI", StringComparison.OrdinalIgnoreCase) ? "https://api.openai.com" : "https://api.anthropic.com"), _storage, _config, _monitor, _sessionCancellation.Token);
            agent.Lore=_lore;
            _agents[npcName] = agent;
        }

        var npc = Game1.getCharacterFromName(npcName);
        var portrait = npc?.Portrait;

        _monitor.Log($"Opening chat with {npcName}", LogLevel.Debug);
        var generation = _generation;
        ChatMenu? menu = null;
        menu = new ChatMenu(agent, portrait, _monitor, (npcNameArg, playerText) =>
        {
            var captured=GameStateProvider.Capture(npcNameArg);
            var snapshot=captured.Json;
            _monitor.Log($"Context capture: {captured.CaptureMilliseconds:F2} ms",LogLevel.Debug);
            using(var state=System.Text.Json.JsonDocument.Parse(snapshot))
                _storage.Observe(npcNameArg,state.RootElement.TryGetProperty("gameDate",out var date)?date.ToString():"unknown",snapshot);
            _ = Task.Run(RespondAsync);
            async Task RespondAsync()
            {
                try
                {
                    var response=await agent.SendAsync(playerText,snapshot);
                    if(generation==_generation)menu!.EnqueueResponse(response);
                }
                catch(Exception ex) { _monitor.Log("Chat request failed: "+ex.GetType().Name,LogLevel.Error); if(generation==_generation)agent.EndRequest("暂时无法回复，请稍后重试。"); }
            }
        },agent.GetGreeting(openingState),()=>GameStateProvider.Capture(npcName).Json,controllerInput);
        Game1.activeClickableMenu=menu;
    }

    public void LoadSave()
    {
        if(!Context.IsMainPlayer)return;
        FlushAll();
        InvalidateLore();
        var data=_helper.Data.ReadSaveData<SaveMemory>("memory-v2") ?? new SaveMemory();
        var saveId=Game1.uniqueIDForThisGame.ToString();var playerId=Game1.player.UniqueMultiplayerID.ToString();
        if((data.SaveId!="" && data.SaveId!=saveId)||(data.PlayerId!="" && data.PlayerId!=playerId))data=new SaveMemory();
        data.SaveId=saveId;data.PlayerId=playerId;
        _storage=new StorageManager(data);
        RefreshLore();
    }
    public void Save()
    {
        if(Context.IsWorldReady && Context.IsMainPlayer)_helper.Data.WriteSaveData("memory-v2",_storage.Export());
    }
    public void Tick() { RefreshLore(); foreach(var agent in _agents.Values)agent.ProcessIdleMemory(); }
    public void ObserveAll()
    {
        if(!Context.IsWorldReady || !Context.IsMainPlayer)return;
        var reset=_storage.ResetVersion;
        foreach(var npc in _agents.Keys.ToArray())
        {
            if(Game1.getCharacterFromName(npc)==null)continue;
            var world=GameStateProvider.GetStateJSON(npc);
            using var doc=System.Text.Json.JsonDocument.Parse(world);
            _storage.Observe(npc,doc.RootElement.TryGetProperty("gameDate",out var d)?d.ToString():"unknown",world);
        }
        ResetIfNeeded(reset);
    }
    public void ResetIfNeeded(int previousVersion)
    {
        if(_storage.ResetVersion==previousVersion)return;
        var snapshot=_storage.Export();FlushAll();_storage=new StorageManager(snapshot);
        if(Game1.activeClickableMenu is ChatMenu)Game1.exitActiveMenu();
    }
    public SaveMemory ExportMemory()=>_storage.Export();
    public void RecordGift(string npc,string item)
    {
        if(!Context.IsMainPlayer || !Context.IsWorldReady)return;
        if(!_personalities.ContainsKey(npc))return;
        var world=GameStateProvider.GetStateJSON(npc);
        using var doc=System.Text.Json.JsonDocument.Parse(world);
        _storage.AddEvidence(npc,new MemoryRecord { Kind="GameEvent", Key="gift", Text="Player gave you "+item, GameTime=doc.RootElement.GetProperty("gameDate").ToString(),EvidenceIds=new(){"gift:"+Guid.NewGuid().ToString("N")} });
    }

    public int ImportLegacy(string npc)
    {
        if(!Context.IsWorldReady||!Context.IsMainPlayer||!IsConfiguredNPC(npc))throw new InvalidOperationException("Load the intended single-player save and choose a configured NPC.");
        var file=Path.Combine(_helper.DirectoryPath,"data",npc.ToLowerInvariant()+"_memory.json");
        if(!File.Exists(file))return 0;
        var old=System.Text.Json.JsonSerializer.Deserialize<NPCMemory>(File.ReadAllText(file));
        if(old==null)return 0;
        var records=old.KeyFacts.Select((text,i)=>new MemoryRecord{Key="legacy."+i,Text=text,Kind="LegacyUnverified",GameTime="unknown",EvidenceIds=new(){"legacy:"+npc+":"+i}}).ToList();
        _storage.MergeStatements(npc,records);
        return records.Count;
    }

    public void FlushAll()
    {
        _sessionCancellation.Cancel();
        _sessionCancellation.Dispose();
        _sessionCancellation=new();
        _generation++;
        InvalidateLore();
        _agents.Clear();
        _storage=new StorageManager(new SaveMemory());
    }
}
