using System;
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

        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;

        Monitor.Log("SV_SOUL loaded. Enabled NPCs: " + string.Join(", ", _config.EnabledNPCs), LogLevel.Info);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.currentLocation == null)
            return;

        if (!e.Button.IsActionButton())
            return;

        // Check if a trigger key is required
        if (_config.TriggerKey != null)
        {
            var triggerKey = Enum.TryParse<Keys>(_config.TriggerKey, true, out var parsed) ? parsed : Keys.None;
            if (triggerKey != Keys.None && !Helper.Input.IsDown((SButton)triggerKey))
                return;
        }

        // Find NPC at cursor position
        var cursorTile = e.Cursor.GrabTile;
        var npc = Game1.currentLocation.isCharacterAtTile(cursorTile);

        if (npc == null)
            return;

        var npcName = npc.Name;

        if (!_agentManager.IsConfiguredNPC(npcName))
            return;

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
        });
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        _agentManager.FlushAll();
    }
}
