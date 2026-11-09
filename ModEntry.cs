using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using System.Text.Json;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using SV_SOUL.Agents;
using SV_SOUL.Config;

namespace SV_SOUL;

public class ModEntry : Mod
{
    private AgentManager _agentManager = null!;
    private ModConfig _config = null!;

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        _agentManager = new AgentManager(helper, Monitor, _config);

        helper.Events.Content.AssetsInvalidated += (_, e) =>
        {
            if(e.NamesWithoutLocale.Any(n=>n.IsEquivalentTo("Data/Characters") || n.IsEquivalentTo("Data/Objects") || n.IsEquivalentTo("Data/PassiveFestivals") || n.BaseName.StartsWith("Data/Festivals/",StringComparison.OrdinalIgnoreCase) || n.BaseName.StartsWith("Strings/",StringComparison.OrdinalIgnoreCase)))
                _agentManager.InvalidateLore();
        };
        helper.Events.GameLoop.SaveLoaded += (_, _) => _agentManager.LoadSave();
        helper.Events.GameLoop.DayStarted += (_, _) => _agentManager.ObserveAll();
        helper.Events.GameLoop.TimeChanged += (_, _) => _agentManager.ObserveAll();
        helper.ConsoleCommands.Add("svsoul_import_legacy", "Explicitly bind old unscoped NPC memories to the loaded save: svsoul_import_legacy Abigail", (_, args) =>
        {
            try { if(args.Length!=1)throw new ArgumentException("Specify one NPC name."); var count=_agentManager.ImportLegacy(args[0]); Monitor.Log($"Imported {count} unverified legacy facts. Original files retained; save normally to commit.",LogLevel.Info); }
            catch(Exception ex){Monitor.Log(ex.Message,LogLevel.Error);}
        });
        GiftObserver.Install(ModManifest.UniqueID, _agentManager, Monitor);
        helper.Events.GameLoop.Saving += (_, _) => _agentManager.Save();
        helper.Events.GameLoop.UpdateTicked += (_, e) => { if(e.IsMultipleOf(60) && Context.IsWorldReady)_agentManager.Tick(); };
        helper.Events.GameLoop.UpdateTicked += OnTestTick;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;

        Monitor.Log("SV_SOUL loaded. Enabled NPCs: " + string.Join(", ", _config.EnabledNPCs), LogLevel.Info);
    }

    private void OnTestTick(object? sender, UpdateTickedEventArgs e)
    {
        if (!_config.EnableTestBridge || !e.IsMultipleOf(30) || !Context.IsWorldReady || !Context.IsMainPlayer) return;
        var path = Path.Combine(Helper.DirectoryPath, "test-request.json");
        if (!File.Exists(path)) return;
        string id = "unknown";
        try
        {
            var command = JsonSerializer.Deserialize<TestCommand>(File.ReadAllText(path))
                ?? throw new InvalidOperationException("Empty test request.");
            File.Delete(path);
            id = command.Id;
            if (Game1.eventUp) throw new InvalidOperationException("Cannot test during an event.");
            if(command.Action == "inspect")
            {
                Helper.Data.WriteJsonFile("test-context.json",new { World=GameStateProvider.Capture(command.Npc),Memory=_agentManager.ExportMemory() });
            }
            else if (command.Action == "open")
            {
                if (!_agentManager.IsConfiguredNPC(command.Npc)) throw new InvalidOperationException("NPC is not configured.");
                var npc = Game1.getCharacterFromName(command.Npc);
                if (npc?.currentLocation == null) throw new InvalidOperationException("NPC not found.");
                if (Game1.activeClickableMenu is UI.ChatMenu) Game1.exitActiveMenu();
                else if (Game1.activeClickableMenu != null || Game1.eventUp) throw new InvalidOperationException("Close the current menu first.");
                Game1.warpFarmer(npc.currentLocation.NameOrUniqueName, (int)npc.Tile.X, (int)npc.Tile.Y + 1, false);
                _agentManager.OpenChat(command.Npc, (_, response) =>
                {
                    if (Game1.activeClickableMenu is UI.ChatMenu menu) menu.EnqueueResponse(response);
                });
            }
            else if(command.Action=="controller")
            {
                if(Game1.activeClickableMenu is not UI.ChatMenu menu || !menu.HandleControllerButton(command.Text))throw new InvalidOperationException("Chat or controller command unavailable.");
            }
            else if(command.Action=="controls")
            {
                if(Game1.activeClickableMenu is not UI.ChatMenu menu)throw new InvalidOperationException("No chat menu.");
                Helper.Data.WriteJsonFile("test-controls.json",menu.DescribeControls());
            }
            else if (command.Action == "send")
            {
                if (Game1.activeClickableMenu is not UI.ChatMenu menu || !menu.SubmitTestMessage(command.Text))
                    throw new InvalidOperationException("Chat is not ready.");
            }
            else if (command.Action == "close")
            {
                if (Game1.activeClickableMenu is UI.ChatMenu) Game1.exitActiveMenu();
            }
            else throw new InvalidOperationException("Unknown test action.");
            WriteTestResult(id, "accepted", null);
        }
        catch (Exception ex)
        {
            if (File.Exists(path)) File.Delete(path);
            WriteTestResult(id, "error", ex.Message);
        }
    }

    private void WriteTestResult(string id, string status, string? error)
    {
        Helper.Data.WriteJsonFile("test-result.json", new { Id = id, Status = status, Error = error });
        Monitor.Log($"Test request {id}: {status} {error}", LogLevel.Info);
    }

    private class TestCommand
    {
        public string Id { get; set; } = "";
        public string Action { get; set; } = "open";
        public string Npc { get; set; } = "Abigail";
        public string Text { get; set; } = "";
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if(Game1.activeClickableMenu is UI.ChatMenu chat && e.Button.TryGetController(out _))
        {
            if(chat.HandleControllerButton(e.Button.ToString()))Helper.Input.Suppress(e.Button);
            return;
        }
        if (!Context.IsWorldReady || Game1.currentLocation == null || Game1.activeClickableMenu != null || Game1.eventUp)
            return;

        bool controllerChat=Enum.TryParse<SButton>(_config.ControllerChatButton,true,out var controllerButton)&&e.Button==controllerButton&&e.Button.TryGetController(out _);
        if (!e.Button.IsActionButton() && !controllerChat)
            return;

        if(_config.TriggerKey != null && !controllerChat)
        {
            if(!Enum.TryParse<SButton>(_config.TriggerKey,true,out var trigger)||!Helper.Input.IsDown(trigger))return;
        }

        // Keep gifts, bouquets and pendants on the vanilla path unless AI chat is explicitly requested.
        bool explicitChat=controllerChat || (Enum.TryParse<SButton>(_config.SocialChatKey,true,out var chatKey)&&Helper.Input.IsDown(chatKey));
        if(Game1.player.ActiveObject!=null && !explicitChat)return;

        // Find NPC at cursor position
        var cursorTile = e.Cursor.GrabTile;
        var npc = Game1.currentLocation.isCharacterAtTile(cursorTile);
        // Explicit modified interaction also reaches a nearby NPC behind a shop counter.
        if(npc==null && explicitChat)
            npc=Game1.currentLocation.characters
                .Where(c=>c is not StardewValley.Monsters.Monster && c is not StardewValley.Characters.Pet && c is not StardewValley.Characters.Horse && c is not StardewValley.Characters.Child)
                .Where(c=>Vector2.DistanceSquared(c.Tile,Game1.player.Tile)<=9)
                .OrderBy(c=>Vector2.DistanceSquared(c.Tile,Game1.player.Tile)).FirstOrDefault();

        if (npc == null || npc is StardewValley.Monsters.Monster || npc is StardewValley.Characters.Pet || npc is StardewValley.Characters.Horse || npc is StardewValley.Characters.Child)
            return;

        var npcName = npc.Name;

        if (!_agentManager.IsConfiguredNPC(npcName))
            return;

        if(_agentManager.PreserveVanilla(npcName))
        {
            if(!explicitChat)return;
        }

        // Suppress default dialogue and open our chat
        Helper.Input.Suppress(e.Button);

        Game1.activeClickableMenu = null; // Clear any existing menu

        _agentManager.OpenChat(npcName, (name, response) =>
        {
            // This callback runs on a background thread after API response
            // We need to enqueue the response for the ChatMenu to pick up
            // The ChatMenu polls _responseQueue in its update() method
            if (Game1.activeClickableMenu is UI.ChatMenu chatMenu)
            {
                chatMenu.EnqueueResponse(response);
            }
        },e.Button.TryGetController(out _));
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        _agentManager.FlushAll();
    }
}
