using System.Net;
using System.Text.Json;
using System.Diagnostics;
using SV_SOUL.Agents;
using SV_SOUL.Config;
using SV_SOUL.Models;
using SV_SOUL.Storage;
using StardewModdingAPI;
if(args.Length==2 && args[0]=="--export-voice"){VoiceFixtures.Export(args[1]);return;}
if(args.Length==2 && args[0]=="--export-profiles"){VoiceFixtures.ExportProfiles(args[1]);return;}
if(args.Length==2 && args[0]=="--export-social"){VoiceFixtures.ExportSocial(args[1]);return;}
if(args.Length==2 && args[0]=="--export-lore"){LoreEvaluation.Export(args[1]);return;}
int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
var handler=new Fake();var config=new ModConfig{ApiProvider="OpenAI",Model="test",MaxHistoryTurns=8};
var store=new StorageManager(new SaveMemory{SaveId="farm-a",PlayerId="player-a"});
var persona=new NPCPersonality{Name="Abigail",DisplayName="Abigail",CoreTraits="Adventurous",StageProfiles=new(){["Married"]="You are the player's wife."}};
NPCAgent Agent(StorageManager s)=>new(persona,new HttpClient(handler),"test-key","https://example.test/v1",s,config,new Monitor());
var agent=Agent(store);
var wife="{\"gameDate\":\"Y4 winter 7 600\",\"relationship\":\"Married\",\"residence\":\"FarmHouse\"}";
store.Observe("Abigail","Y4 winter 7",wife);
Check(agent.BuildSystemPrompt(wife).Contains("wife"),"married stage context");
Check(agent.BuildSystemPrompt(wife).Contains("Unknown dates"),"unknown dates protected");
for(int i=0;i<10;i++)Check(await agent.SendAsync("我喜欢钓鱼",wife)=="OK","reply "+i);
await handler.ExtractionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
Check(!agent.MemoryWork.IsCompleted,"slow extraction does not block reply");
handler.ExtractionRelease.SetResult(true);await agent.MemoryWork;
Check(handler.Extractions==1,"one background extraction");
Check(store.LoadMemories("Abigail").Records.Any(r=>r.Kind=="PlayerStatement"&&r.Text.Contains("钓鱼")),"evidence-backed extraction");
var original=store.LoadHistory("Abigail")[0];
await agent.SendAsync("之后呢",wife);
Check(handler.LastCount<=18,"bounded prompt history");
Check(store.LoadHistory("Abigail")[0].Id==original.Id&&store.LoadHistory("Abigail")[0].Timestamp==original.Timestamp,"stable evidence and timestamp");
handler.FailNext=true;var before=store.LoadHistory("Abigail").Count;await agent.SendAsync("failure",wife);
Check(store.LoadHistory("Abigail").Count==before,"failed request not persisted");
var saved=store.Export();store.AddEvidence("Abigail",new MemoryRecord{Text="unsaved future",Kind="GameEvent"});
var restored=new StorageManager(saved);
Check(!restored.LoadMemories("Abigail").Records.Any(r=>r.Text=="unsaved future"),"rollback restores saved memory");
Check(new StorageManager(new SaveMemory{SaveId="farm-b"}).LoadMemories("Abigail").Records.Count==0,"save isolation");
Check(restored.LoadMemories("Haley").Records.Count==0,"NPC isolation");
restored.Observe("Abigail","Y4 winter 8","{\"relationship\":\"Divorced\",\"residence\":\"unknown\"}");
var relation=restored.LoadMemories("Abigail").Records.Where(r=>r.Key=="relationship").ToList();
Check(relation.Count==2&&relation.Count(r=>r.Active)==1&&relation[0].ValidUntil!=null,"temporal supersession");
var invalid=new Fake{InvalidEvidence=true};var badStore=new StorageManager(new SaveMemory());
var badAgent=new NPCAgent(persona,new HttpClient(invalid),"test-key","https://example.test/v1",badStore,config,new Monitor());
invalid.ExtractionRelease.SetResult(true);
for(int i=0;i<10;i++)await badAgent.SendAsync("hello",wife);
await badAgent.MemoryWork;
Check(badStore.LoadMemories("Abigail").Records.Count==0,"invented evidence rejected");
config.ApiProvider="Anthropic";handler.Anthropic=true;
Check(await agent.SendAsync("anthropic",wife)=="OK","Anthropic compatibility");
using var search=new MemoryRetriever();
var records=Enumerable.Range(0,10000).Select(i=>new MemoryRecord{Text="普通事件 "+i,RecordedAt=DateTime.UtcNow.AddDays(-1)}).ToList();
records[0].Text="玩家喜欢钓鱼 fishing";records[1].Text="玩家喜欢紫水晶 amethyst";records[2].Text="玩家喜欢吃南瓜 pumpkin";
search.Recall(records,"warmup");var times=new List<double>();
foreach(var term in new[]{"钓鱼","fishing","紫水晶","amethyst","南瓜","pumpkin"})Check(search.Recall(records,term).Any(r=>r.Text.Contains(term)),"bilingual retrieval "+term);
for(int i=0;i<100;i++){var watch=Stopwatch.StartNew();search.Recall(records,"钓鱼");times.Add(watch.Elapsed.TotalMilliseconds);}
Console.WriteLine($"Warm 10k recall P95: {times.OrderBy(x=>x).ElementAt(94):F2} ms");
var chronicle=new List<MemoryRecord>{
 new(){Text="昨天收到紫水晶",GameTime="Y1 spring 28 1200",Active=true},
 new(){Text="今天收到南瓜",GameTime="Y1 summer 1 1000",Active=true},
 new(){Text="以前住在商店",GameTime="Y1 spring 1 600",Active=false,ValidUntil="Y1 spring 28"}
};
var now="{\"gameDate\":\"Y1 summer 1 1200\"}";
Check(search.Recall(chronicle,"昨天",8,now).Count==1,"game calendar season boundary");
Check(search.Recall(chronicle,"昨天",8,"{\"gameDate\":\"Y1 Summer 1 1200\"}").Count==1,"enum season capitalization compatibility");
Check(search.Recall(chronicle,"以前住哪里",8,now).Any(r=>!r.Active),"historical facts can be recalled");
Check(search.Recall(chronicle,"现在住哪里",8,now).All(r=>r.Active),"obsolete facts excluded from current recall");
var largePersona=new NPCPersonality{Name="Abigail",CoreTraits=new string('A',12000),StageProfiles=new(){["Married"]="You are the player's wife."}};
var budgetAgent=new NPCAgent(largePersona,new HttpClient(handler),"test-key","https://example.test/v1",new StorageManager(new SaveMemory()),config,new Monitor());
var bounded=budgetAgent.BuildSystemPrompt(wife);
Check(bounded.Length<=8000&&bounded.Contains("已经结婚")&&bounded.Contains("Chinese"),"long biography cannot evict facts or language");
var copy=store.Export();copy.Npcs.Clear();Check(store.Export().Npcs.Count>0,"export is detached snapshot");
var wipe=new StorageManager(new SaveMemory());wipe.Observe("Abigail","Y1 spring 2","{\"relationship\":\"Divorced\"}");wipe.AddEvidence("Abigail",new MemoryRecord{Text="old private history"});wipe.Observe("Abigail","Y1 spring 3","{\"relationship\":\"Friendly\"}");
Check(wipe.ResetVersion==1&&!wipe.LoadMemories("Abigail").Records.Any(r=>r.Text=="old private history"),"relationship memory reset");
using var canceled=new CancellationTokenSource();canceled.Cancel();var canceledAgent=new NPCAgent(persona,new HttpClient(handler),"test-key","https://example.test/v1",store,config,new Monitor(),canceled.Token);
try{await canceledAgent.SendAsync("late");throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"old session cancellation");}
var ordered=new[]{new MemoryRecord{Text="game yesterday",GameTime="Y1 spring 1 2600",RecordedAt=DateTime.UtcNow},new MemoryRecord{Text="game today",GameTime="Y1 spring 2 600",RecordedAt=DateTime.UtcNow.AddDays(-10)}};
Check(search.Recall(ordered,"",1).Single().Text=="game today","recency follows game time before real time");
Check(!bounded.Contains("friendshipPoints"),"dialogue scene hides raw stat fields");
Check(DialogueVoice.Scene(JsonDocument.Parse("{\"relationship\":\"Roommate\"}").RootElement).Contains("不是夫妻"),"roommate remains distinct from marriage");
Check(DialogueVoice.Direction.Contains("已经完成"),"subjective wishes remain distinct from completed actions");
var voiceCard=new NPCPersonality{DialogueExamples=new(){new(){Topic="marriage",Relationship="Married",Cues=new[]{"关系"},User="关系",Reply="另一半"},new(){Topic="quiet",User="你好",Reply="我在听"}}};
Check(!DialogueVoice.Examples(voiceCard,"关系","Friendly").Contains("另一半"),"marriage examples do not leak to strangers");
Check(DialogueVoice.Examples(voiceCard,"关系","Married").Contains("另一半"),"marriage example selected for spouse");
var profiles=Directory.GetFiles("assets/personalities","*.json").Select(p=>JsonSerializer.Deserialize<NPCPersonality>(File.ReadAllText(p))!).ToList();
foreach(var card in profiles)
{
 bool romance=card.RelationshipMode=="Romance";
 Check(card.DialogueExamples.Count>=(romance?10:6)&&!string.IsNullOrWhiteSpace(card.BehavioralProfile),card.Name+" complete profile");
 var npcAgent=new NPCAgent(card,new HttpClient(handler),"test-key","https://example.test/v1",new StorageManager(new SaveMemory()),new ModConfig(),new Monitor());
 var world="{\"relationship\":\"Married\",\"timeOfDay\":2200}";
 var prompt=npcAgent.BuildSystemPrompt(world,"我们是什么关系");
 if(romance)
 {
  Check(prompt.Contains("已经结婚"),card.Name+" romance context");
  Check(card.StageProfiles.ContainsKey("Divorced")&&card.SituationGreetings.ContainsKey("Married:night"),card.Name+" relationship stages");
 }
 else
 {
  Check(!prompt.Contains("你与当前玩家已经结婚"),card.Name+" rejects unconfirmed romance");
  Check(card.DialogueExamples.All(e=>e.Relationship!="Married")&&!card.StageProfiles.ContainsKey("Married")&&card.PreserveVanillaInteraction,card.Name+" social profile preserves vanilla");
 }
}
var social=new NPCPersonality{Name="Robin",RelationshipMode="Social"};
using(var normalized=JsonDocument.Parse(RelationshipPolicy.Normalize("{\"relationship\":\"Dating\",\"canBeRomanced\":false}",social)))
 Check(normalized.RootElement.GetProperty("relationship").GetString()=="Friendly","non-romance flirting does not change relationship");
using(var normalized=JsonDocument.Parse(RelationshipPolicy.Normalize("{\"relationship\":\"Married\",\"isPlayerSpouse\":true,\"canBeRomanced\":false}",social)))
 Check(normalized.RootElement.GetProperty("relationshipMode").GetString()=="Romance","confirmed adult modded marriage respected");
using(var normalized=JsonDocument.Parse(RelationshipPolicy.Normalize("{\"relationship\":\"Married\",\"isPlayerSpouse\":true,\"canBeRomanced\":true,\"isChild\":true}",social)))
 Check(normalized.RootElement.GetProperty("relationshipMode").GetString()=="Child"&&normalized.RootElement.GetProperty("relationship").GetString()=="Friendly","child policy takes priority");
using(var normalized=JsonDocument.Parse(RelationshipPolicy.Normalize("{\"relationship\":\"Roommate\",\"weddingDate\":\"Y4 winter 7\"}",new NPCPersonality{Name="Krobus",RelationshipMode="Roommate"})))
 Check(!normalized.RootElement.TryGetProperty("weddingDate",out _)&&normalized.RootElement.TryGetProperty("cohabitationDate",out _),"roommate date is not a wedding");
Check(profiles.Count==48&&profiles.Select(p=>p.Name).Distinct().Count()==48,"all 48 catalogue profiles are unique");
Check(profiles.Count(p=>p.RelationshipMode=="Romance")==12,"twelve romance profiles separated");
Check(profiles.Count(p=>p.RelationshipMode=="Child")==3,"three child profiles separated");
Check(new ModConfig().EnabledNPCs.SequenceEqual(new[]{"*"}),"all NPC mode enabled by default");
Check(DialogueVoice.Situations(JsonDocument.Parse("{\"isBirthday\":true,\"isFestival\":true,\"giftsToday\":1,\"isRaining\":true}").RootElement).Count==4,"birthday festival gift weather triggers");
var unhappy=DialogueVoice.Scene(JsonDocument.Parse("{\"relationship\":\"Married\",\"friendshipHearts\":0}").RootElement);
Check(unhappy.Contains("已经结婚")&&unhappy.Contains("好感较低")&&!unhappy.Contains("尚不熟悉"),"low-affection marriage preserves identity without assuming intimacy");
Check(DialogueVoice.Scene(JsonDocument.Parse("{\"relationship\":\"NoFriendshipRecord\"}").RootElement).Contains("不默认初次见面"),"service NPC without hearts is not automatically a stranger");
using(var childHistory=JsonDocument.Parse(RelationshipPolicy.Normalize("{\"relationship\":\"Divorced\",\"isChild\":true}",social)))Check(childHistory.RootElement.GetProperty("relationship").GetString()=="Friendly","children do not acquire romantic history");
foreach(var card in profiles)
{
 var choices=QuickReplies.Create(card,"{\"relationship\":\"Friendly\"}",true,false);
 Check(choices.Select(x=>x.Id).Distinct().Count()==choices.Count&&choices.All(x=>!string.IsNullOrWhiteSpace(x.Text)),card.Name+" valid quick choices");
 Check(choices.All(x=>x.Id!="affection"&&x.Id!="roommate"),card.Name+" no assumed relationship quick choice");
}
var contextual=QuickReplies.Create(new NPCPersonality{RelationshipMode="Romance"},"{\"relationship\":\"Married\",\"isBirthday\":true,\"isRaining\":true}",true,true);
Check(contextual.Any(c=>c.Id=="birthday")&&contextual.Any(c=>c.Id=="rain")&&contextual.Any(c=>c.Id=="affection")&&contextual.Any(c=>c.Id=="more"),"context and follow-up choices");
var roommateChoices=QuickReplies.Create(new NPCPersonality{Name="Krobus",RelationshipMode="Roommate"},"{\"relationship\":\"Roommate\"}",false,false);
Check(roommateChoices.Any(c=>c.Id=="roommate")&&roommateChoices.All(c=>c.Id!="affection"),"non-romantic roommate choices");
var pager=new QuickReplyPager();pager.SetItems(contextual);pager.Move(1,0);Check(pager.Selected==1,"controller right navigation");pager.Move(0,1);Check(pager.Selected==3,"controller down navigation");pager.NextPage();Check(pager.Selected==0&&pager.Page==1,"next page resets selection");pager.SetPageSize(2);Check(pager.Visible.Count<=2&&pager.Current!=null,"compact viewport choices");
pager.SetItems(new[]{new QuickReply("only","one")});pager.Move(1,1);Check(pager.Selected==0,"partial page has no invisible focus");pager.NextPage();Check(pager.Page==0,"one page wraps safely");pager.SetItems(Array.Empty<QuickReply>());Check(pager.Current==null,"empty selection is safe");
var invited=QuickReplies.Create(new NPCPersonality(),"{}",true,true,"愿意和我一起去吗？");
Check(invited.Any(c=>c.Id=="agree")&&invited.Any(c=>c.Id=="decline"),"invitation offers acceptance and refusal");
Check(agent.ConversationView(4).Messages.Count<=4,"history page bounded");
Check(agent.BeginRequest("pending"),"shared pending begins");
Check(!agent.BeginRequest("duplicate"),"shared pending rejects duplicate");
Check(agent.ConversationView(50).Pending && agent.ConversationView(50).Draft=="pending","reopened view restores pending");
var revision=agent.ViewRevision;agent.EndRequest("failed");
Check(agent.ViewRevision>revision && !agent.ConversationView(50).Pending && agent.ConversationView(50).Error=="failed","shared failure revision");
using(var lore=new LoreRetriever(new[]{
 new LoreEntry{Id="town",Text="鹈鹕镇是居民生活的城镇",Aliases=new[]{"鹈鹕镇","Pelican Town"},Source="test"},
 new LoreEntry{Id="private",Kind="family",Text="我的家人",Aliases=new[]{"家人"},KnownBy=new[]{"Abigail"},Source="test"},
 new LoreEntry{Id="locked",Text="鹈鹕镇秘密",Aliases=new[]{"鹈鹕镇"},Condition="unknown",Source="test"}}))
{
 Check(lore.Recall("今天有点难过","Abigail")=="","emotional chat needs no lore");
 Check(lore.Recall("介绍鹈鹕镇","Abigail").Contains("[town]"),"background entity retrieval");
 Check(lore.Recall("Pelican Town","Abigail").Contains("[town]"),"English alias retrieval");
 Check(!lore.Recall("鹈鹕镇","Abigail").Contains("locked"),"unsupported condition excluded");
 Check(lore.Recall("家人","Abigail").Contains("private") && lore.Recall("家人","Sam")=="","knowledge owner isolation");
 agent.Lore=lore;Check(agent.BuildSystemPrompt(wife,"鹈鹕镇").Contains("[town]"),"background enters prompt");agent.Lore=null;
}
using(var conditionalLore=new LoreRetriever(new[]{
 new LoreEntry{Id="secret",Text="矿井中的往事",Aliases=new[]{"矿井"},KnownBy=new[]{"Abigail"},Condition="event:42",Source="fixture"},
 new LoreEntry{Id="town",Text="鹈鹕镇的背景",Aliases=new[]{"鹈鹕镇"},Source="fixture"}}))
{
 var locked=LoreQueryContext.FromSnapshot("Abigail","{}");
 var unlocked=LoreQueryContext.FromSnapshot("Abigail","{\"seenEvents\":[\"42\"]}");
 Check(conditionalLore.Recall("矿井",locked)=="","unseen event not recalled");
 Check(conditionalLore.Recall("矿井",unlocked).Contains("secret"),"seen event permits owner knowledge");
 Check(conditionalLore.Recall("矿井",unlocked with {Npc="Sam"})=="","player progression does not give all NPCs knowledge");
 Check(conditionalLore.Recall("矿井",locked)=="","shared index does not leak prior save permission");
 Check(conditionalLore.Recall("那里呢",locked with {RecentText="介绍鹈鹕镇"}).Contains("town"),"followup resolves recent topic");
 Check(conditionalLore.Recall("今天心情不好",locked with {RecentText="介绍鹈鹕镇"})=="","unrelated emotional message does not inherit topic");
}
using(var largeLore=new LoreRetriever(Enumerable.Range(0,10000).Select(i=>new LoreEntry{Id="entry."+i,Text="资料条目 "+i,Aliases=new[]{"实体"+i},Source="benchmark"})))
{
 largeLore.Recall("实体9999","Abigail");var timings=new List<double>();
 for(int i=0;i<200;i++){var timer=Stopwatch.StartNew();var result=largeLore.Recall("实体9999","Abigail");timer.Stop();timings.Add(timer.Elapsed.TotalMilliseconds);if(i==0)Check(result.Contains("entry.9999"),"full index includes documents beyond prior 512 cap");}
 timings.Sort();var p95=timings[(int)(timings.Count*0.95)];Console.WriteLine($"Lore 10000 documents warm P95: {p95:F3} ms");Check(p95<50,"warm retrieval P95 below 50ms");
}
var buildStarted=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
var releaseBuild=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
using(var host=new LoreIndexHost(entries=>{buildStarted.TrySetResult(true);releaseBuild.Task.GetAwaiter().GetResult();return new LoreRetriever(entries);}))
{
 var inputs=new List<LoreEntry>{new(){Id="async",Text="鹈鹕镇资料",Aliases=new[]{"鹈鹕镇"},Source="fixture"}};
 Check(host.Start(inputs),"background build scheduled");await buildStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
 inputs[0].Text="MUTATED";
 Check(host.Recall("鹈鹕镇","Abigail")=="" && !host.IsReady,"chat remains available during blocked build");
 Check(!host.Start(inputs),"concurrent rebuild coalesced");host.Invalidate();releaseBuild.TrySetResult(true);
 var deadline=DateTime.UtcNow.AddSeconds(5);while(host.IsBuilding && DateTime.UtcNow<deadline){host.Poll(out _);await Task.Delay(5);}
 Check(!host.IsBuilding && !host.IsReady,"stale build discarded after invalidation");
 inputs[0].Text="鹈鹕镇新资料";host.Start(inputs);inputs[0].Text="MUTATED";
 deadline=DateTime.UtcNow.AddSeconds(5);while(host.IsBuilding && DateTime.UtcNow<deadline){host.Poll(out _);await Task.Delay(5);}
 Check(host.IsReady && host.Recall("鹈鹕镇","Abigail").Contains("新资料"),"detached background snapshot published");
 host.Invalidate();Check(host.Recall("鹈鹕镇","Abigail")=="","invalidated content cannot be recalled");
}
var backgroundFiles=Directory.GetFiles("assets/lore","*.json");
var backgroundEntries=backgroundFiles.SelectMany(file=>JsonSerializer.Deserialize<List<LoreEntry>>(File.ReadAllText(file))!).ToList();
Check(backgroundEntries.Select(e=>e.Id).Distinct().Count()==backgroundEntries.Count,"bundled lore IDs unique");
Check(backgroundEntries.All(e=>e.Aliases.Length>0 && (e.Source.StartsWith("https://stardewvalleywiki.com/")||e.Source.StartsWith("game:"))),"bundled background provenance present");
using(var bundled=new LoreRetriever(backgroundEntries))
{
 foreach(var pair in new[]{("鱼店在哪","beach.shop"),("木匠是谁","mountain.carpenter"),("猪车卖什么","forest.cart"),("百乐汤是什么","festival.luau"),("哪里有珊瑚","beach.tide")})
  Check(bundled.Recall(pair.Item1,"Abigail").Contains("["+pair.Item2+"]"),"bundled question: "+pair.Item1);
}
// Retrieval policies use independent fixtures rather than only happy-path corpus lookups.
using(var policies=new LoreRetriever(new[]{
 new LoreEntry{Id="both",Text="秘密往事",Aliases=new[]{"秘密往事"},KnownBy=new[]{"Abigail"},RequiredAll=new[]{"event:1","hearts:6"},RequiredAny=new[]{"mail:a","mail:b"},Source="fixture"},
 new LoreEntry{Id="wrongversion",Text="秘密往事",Aliases=new[]{"秘密往事"},GameVersion="1.7",Source="fixture"},
 new LoreEntry{Id="substring",Text="Sam is a person",Aliases=new[]{"Sam"},Source="fixture"}},"1.6.15"))
{
 var context=LoreQueryContext.FromSnapshot("Abigail","{\"seenEvents\":[\"1\"],\"friendshipHearts\":6,\"receivedMail\":[\"b\"]}");
 Check(policies.Recall("秘密往事",context).Contains("both"),"AND and OR disclosure conditions satisfied");
 Check(policies.Recall("秘密往事",context with {Conditions=new HashSet<string>{"event:1","mail:b"}})=="","missing condition blocks disclosure");
 Check(!policies.Recall("秘密往事",context).Contains("wrongversion"),"incompatible game version excluded");
 Check(policies.Recall("same old day","Abigail")=="","English entity aliases respect word boundaries");
 var held=policies.Retrieve("秘密往事",context with {Conditions=new HashSet<string>()});
 Check(held.HasWithheldKnowledge && held.Hits.Count==0,"withheld knowledge signaled without disclosing facts");
 Check(!policies.Retrieve("秘密往事",context with {Npc="Sam"}).HasWithheldKnowledge,"unknown NPC receives no private knowledge hint");
}
var pureNative=LoreDataBuilder.Build(new[]{new CharacterLoreSource("Abigail","阿比盖尔","Fall",13,new(){["卡洛琳"]="母亲"})},Array.Empty<ItemLoreSource>(),Array.Empty<FestivalLoreSource>(),"1.6.15","zh-CN");
using(var structured=new LoreRetriever(pureNative))
{
 Check(structured.Recall("你妈妈是谁","Abigail").Contains("卡洛琳"),"implicit self family question");
 Check(structured.Recall("你生日是哪天","Abigail").Contains("13"),"implicit self birthday question");
 Check(structured.Recall("我妈妈今天很难过","Abigail")=="","player family does not retrieve NPC family");
 Check(structured.Recall("阿比盖尔的生日","Sam")=="","self knowledge remains scoped");
}
if(args.Contains("--native-lore"))
{
 var native=LoreFixtures.LoadNative("tools");var all=backgroundEntries.Concat(native).ToList();
 Check(native.Count(e=>e.Kind=="calendar")==12,"all twelve native festival calendars");
 Check(native.Count(e=>e.Kind=="item")>150,"native common item descriptions populated");
 Check(native.All(e=>!e.Text.Contains("[LocalizedText")),"native localized strings fully resolved");
 Check(native.All(e=>e.Source.StartsWith("game:")&&e.GameVersion=="1.6.15"),"native provenance and version retained");
 using var retrieval=new LoreRetriever(all,"1.6.15");
 foreach(var example in new[]{("你生日是哪天","Abigail","birthday.Abigail"),("你妈妈是谁","Abigail","family.Abigail"),("紫水晶是什么","Abigail","object.66"),("南瓜是什么","Penny","object.276"),("复活节什么时候","Abigail","calendar.spring13"),("夜市什么时候","Willy","calendar.NightMarket")})
  Check(retrieval.Recall(example.Item1,example.Item2).Contains("["+example.Item3+"]"),"native corpus: "+example.Item1);
 var budgetHandler=new Fake();var budgetStore=new StorageManager(new SaveMemory());
 budgetStore.AddEvidence("Abigail",new MemoryRecord{Kind="PlayerStatement",Text="玩家明确喜欢钓鱼",Key="preference.fishing"});
 var budgetCard=JsonSerializer.Deserialize<NPCPersonality>(File.ReadAllText("assets/personalities/abigail.json"))!;
 var liveBudgetAgent=new NPCAgent(budgetCard,new HttpClient(budgetHandler),"test-key","https://example.test/v1",budgetStore,new ModConfig{ApiProvider="OpenAI",Model="test",MaxHistoryTurns=8},new Monitor()){Lore=retrieval};
 for(int turn=0;turn<6;turn++)
 {
  Check(await liveBudgetAgent.SendAsync("紫水晶 "+new string('字',890),wife)=="OK","budget fixture request succeeds "+turn);
  Check(budgetHandler.LastCharacters<=8000,"total API prompt budget with long history "+turn);
 }
 Check(budgetHandler.LastSystem.Contains("玩家明确喜欢钓鱼")&&budgetHandler.LastSystem.Contains("object.66")&&budgetHandler.LastSystem.Contains("Dialogue direction"),"personal memory, lore and voice coexist under pressure");
 using(var conflicts=new LoreRetriever(new[]{new LoreEntry{Id="curated",FactKey="fact",Text="旧资料",Aliases=new[]{"资料"},Source="https://example.test"},new LoreEntry{Id="native",FactKey="fact",Text="当前游戏资料",Aliases=new[]{"资料"},Source="game:fixture"}}))
  Check(conflicts.Search("资料",new LoreQueryContext("Abigail",new HashSet<string>())).Single().Id=="native","effective assets override same curated fact");
 using(var labelled=JsonDocument.Parse(File.ReadAllText("tests/fixtures/lore-queries.json")))
 {
  var failures=new List<string>();int passed=0;var details=new List<object>();
  foreach(var c in labelled.RootElement.EnumerateArray())
  {
   var npc=c.GetProperty("Npc").GetString()!;var query=c.GetProperty("Query").GetString()!;
   var context=LoreQueryContext.FromSnapshot(npc,c.TryGetProperty("World",out var w)?w.GetRawText():"{}",c.TryGetProperty("Recent",out var r)?r.GetString()!:"");
   var hits=retrieval.Search(query,context);var ids=hits.Select(h=>h.Id).ToArray();
   bool ok=c.TryGetProperty("NoHit",out var noHit)&&noHit.GetBoolean()?hits.Count==0:c.GetProperty("ExpectedIds").EnumerateArray().Any(e=>ids.Contains(e.GetString()));
   if(ok)passed++;else failures.Add(npc+": "+query+" => "+string.Join(",",ids));
   details.Add(new{npc,query,ok,ids});
   Check(hits.Count<=3&&string.Join("\n",hits.Select(h=>"["+h.Id+"] "+h.Text)).Length<=1000,"retrieval budget for "+query);
  }
  File.WriteAllText("artifacts/tests/lore-quality.json",JsonSerializer.Serialize(new{passed,total=labelled.RootElement.GetArrayLength(),failures,details},new JsonSerializerOptions{WriteIndented=true}));
  Check(failures.Count==0,"labelled real corpus retrieval: "+string.Join("; ",failures));
 }
 Console.WriteLine($"Native corpus: {native.Count} entries; item={native.Count(e=>e.Kind=="item")}; calendar={native.Count(e=>e.Kind=="calendar")}; combined={retrieval.Count}");
}
var passiveDates=new[]{new PassiveFestivalContext("NightMarket","Winter",15,17,1700,true),new PassiveFestivalContext("DesertFestival","Spring",15,17,1000,false)};
Check(FestivalContext.Active(passiveDates,"winter",15).Single().Name=="NightMarket","passive festival starts on first day");
Check(FestivalContext.Active(passiveDates,"Winter",17).Count==1&&FestivalContext.Active(passiveDates,"Winter",18).Count==0,"passive festival end-day boundary");
Check(FestivalContext.Active(passiveDates,"Spring",15).Count==0,"unavailable passive festival excluded from today");
Console.WriteLine($"{checks} checks passed.");

class Monitor:IMonitor{public void Log(string text,LogLevel level){}}
class Fake:HttpMessageHandler
{
 public bool FailNext,Anthropic,InvalidEvidence;public int Extractions,LastCount,LastCharacters;public string LastSystem="";
 public TaskCompletionSource<bool> ExtractionStarted=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public TaskCompletionSource<bool> ExtractionRelease=new(TaskCreationOptions.RunContinuationsAsynchronously);
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)
 {
  using var doc=JsonDocument.Parse(await r.Content!.ReadAsStringAsync());
  if(Anthropic){if(!r.Headers.Contains("x-api-key")||!doc.RootElement.TryGetProperty("system",out _))throw new Exception("Anthropic body");return Reply("OK",true);}
  if(r.RequestUri!.AbsoluteUri!="https://example.test/v1/chat/completions"||r.Headers.Authorization?.ToString()!="Bearer test-key")throw new Exception("OpenAI request");
  if(FailNext){FailNext=false;return new(HttpStatusCode.Unauthorized){Content=new StringContent("invalid key")};}
  var messages=doc.RootElement.GetProperty("messages");LastCount=messages.GetArrayLength();LastCharacters=messages.EnumerateArray().Sum(m=>m.GetProperty("content").GetString()!.Length);LastSystem=messages[0].GetProperty("content").GetString()!;
  if(messages[0].GetProperty("content").GetString()!.Contains("You extract"))
  {
   Extractions++;ExtractionStarted.TrySetResult(true);await ExtractionRelease.Task;
   var prompt=messages[1].GetProperty("content").GetString()!;
   var json=prompt[(prompt.IndexOf("Inputs: ")+8)..];using var batch=JsonDocument.Parse(json);
   var id=InvalidEvidence?"invented":batch.RootElement[0].GetProperty("Id").GetString();
   return Reply(JsonSerializer.Serialize(new[]{new{key="preference.fishing",text="玩家喜欢钓鱼",evidenceIds=new[]{id}}}));
  }
  return Reply("OK");
 }
 static HttpResponseMessage Reply(string text,bool anthropic=false)=>new(HttpStatusCode.OK){Content=new StringContent(anthropic?JsonSerializer.Serialize(new{content=new[]{new{type="text",text}}}):JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=text}}}}))};
}
