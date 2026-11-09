using System;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
namespace SV_SOUL.Agents;
internal static class GiftObserver
{
    private static AgentManager? manager;
    private static IMonitor? monitor;
    internal sealed class Attempt { public Farmer Player = null!; public int Count; public string Item = ""; }
    public static void Install(string id,AgentManager target,IMonitor log)
    {
        manager=target;monitor=log;
        try { var method=AccessTools.Method(typeof(NPC),"receiveGift"); if(method==null)throw new MissingMethodException("NPC.receiveGift"); new Harmony(id).Patch(method,new HarmonyMethod(typeof(GiftObserver),nameof(Before)),new HarmonyMethod(typeof(GiftObserver),nameof(After))); }
        catch(Exception ex){log.Log("Gift observation unavailable: "+ex.Message,LogLevel.Warn);}
    }
    private static void Before(NPC __instance,object[] __args,out Attempt? __state)
    {
        __state=null;
        try
        {
            var player=__args.OfType<Farmer>().FirstOrDefault();var item=__args.OfType<Item>().FirstOrDefault();
            if(player!=Game1.player||item==null||player==null)return;
            __state=new Attempt { Player=player,Item=item.DisplayName+" ("+item.QualifiedItemId+")",Count=player.friendshipData.TryGetValue(__instance.Name,out var f)?f.GiftsToday:0 };
        }catch(Exception ex){monitor?.Log("Gift observation skipped: "+ex.GetType().Name,LogLevel.Debug);}
    }
    private static void After(NPC __instance,Attempt? __state)
    {
        try { if(__state!=null && __state.Player.friendshipData.TryGetValue(__instance.Name,out var f)&&f.GiftsToday>__state.Count)manager?.RecordGift(__instance.Name,__state.Item); }
        catch(Exception ex){monitor?.Log("Gift observation skipped: "+ex.GetType().Name,LogLevel.Debug);}
    }
}
