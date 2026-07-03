# SV_SOUL

<p align="center">
  <a href="README.md">中文</a> · <a href="README_EN.md">English</a>
</p>

AI-powered chat NPCs for Stardew Valley. SV_SOUL gives in-game NPCs AI dialogue, character-specific personas, persistent memory, real-time game-state awareness, and a native-style chat UI.

## Features

- **Independent agents**: each NPC is an independent AI instance with its own personality, speaking style, and memory.
- **Real-time awareness**: NPCs can respond to the current season, weather, time, location, and friendship level.
- **Persistent memory**: NPCs remember conversations and experiences across game restarts.
- **Native-style UI**: full-screen chat bubble interface with portraits, scrolling, and thinking animation.
- **Five built-in characters**: Abigail, Shane, Sebastian, Elliott, and Haley.
- **Easy extension**: add a JSON personality file to enable AI dialogue for another NPC.

## Branches

| Branch | Purpose | Contents |
| --- | --- | --- |
| `main` | Source code | Full project source for developers |
| `release` | Player release | Built mod files ready to install |

## Player Installation (`release` branch)

1. Install [SMAPI](https://smapi.io/) 4.0.0+.
2. Download the `SV_SOUL` folder from the `release` branch.
3. Place the `SV_SOUL` folder under `Stardew Valley/Mods/`.
4. Start the game once so SMAPI generates `config.json`.
5. Edit `Stardew Valley/Mods/SV_SOUL/config.json` and set your API key:

```json
{
  "ApiKey": "your-api-key",
  "Model": "claude-sonnet-4-20250514"
}
```

6. Enter the game, approach a supported NPC, and interact to start chatting.

## Developer Build (`main` branch)

### Requirements

- .NET 6.0 SDK
- Stardew Valley 1.6+ for automatic game assembly references

### Build

```bash
git clone https://github.com/JamesRaoXiaoJian/SV_Soul.git
cd SV_Soul
dotnet build
```

After building:

- DLL output: `bin/Debug/net6.0/SV_SOUL.dll`
- Auto-deploy target: `Stardew Valley/Mods/SV_SOUL/`
- Release zip: `bin/Debug/net6.0/`

## Project Structure

```text
SV_Soul/
├── ModEntry.cs                  # SMAPI entry and event hooks
├── SV_SOUL.csproj               # Project file
├── SV_SOUL.sln                  # Solution
├── manifest.json                # SMAPI mod metadata
├── Agents/
│   ├── AgentManager.cs          # Agent lifecycle management
│   ├── NPCAgent.cs              # NPC AI core: prompts, API calls, memory extraction
│   └── GameStateProvider.cs     # Reads real-time game state
├── Config/
│   └── ModConfig.cs             # Config model
├── Models/
│   └── NPCPersonality.cs        # NPC personality schema
├── Storage/
│   ├── StorageManager.cs        # JSON persistence
│   ├── NPCMemory.cs             # Memory schema
│   └── ConversationTurn.cs      # Conversation turn schema
├── UI/
│   ├── ChatMenu.cs              # Full-screen chat bubble UI
│   └── TextInputBox.cs          # Text input widget
└── assets/
    └── personalities/           # NPC personality JSON files
        ├── abigail.json
        ├── shane.json
        ├── sebastian.json
        ├── elliott.json
        └── haley.json
```

## Configuration

Edit `config.json`, generated after the first run:

| Option | Description | Default |
| --- | --- | --- |
| `ApiKey` | API key | `""` |
| `BaseUrl` | API base URL; `null` uses the official endpoint | `null` |
| `EnabledNPCs` | NPCs with AI dialogue enabled | All 5 built-in NPCs |
| `MaxTokens` | Maximum tokens per reply | `200` |
| `Temperature` | Response randomness | `0.9` |
| `MaxHistoryTurns` | Maximum conversation turns kept in history | `50` |
| `Model` | Model identifier | `mimo-v2.5-pro` |
| `Language` | Default dialogue language | `zh` |
| `TriggerKey` | Key that must be held to trigger chat; `null` means direct interaction | `null` |
| `DebugMode` | Debug logging switch | `false` |

### Proxy API

```json
{
  "BaseUrl": "https://your-proxy.example.com/anthropic",
  "Model": "your-model-name"
}
```

API calls follow the Anthropic Messages API format. The endpoint is `{BaseUrl}/v1/messages`.

## Usage

1. Approach a supported NPC: Abigail, Shane, Sebastian, Elliott, or Haley.
2. Press the interaction key, usually Space or right mouse button.
3. Type a message in the chat UI and press Enter.
4. Press Esc to close the UI.

| Action | Key |
| --- | --- |
| Send message | Enter |
| Close chat | Esc |
| Scroll history | Mouse wheel |
| Focus input box | Mouse click |

## Add a Custom NPC

Create a JSON file under `assets/personalities/`:

```json
{
  "Name": "Penny",
  "DisplayName": "Penny",
  "SystemPrompt": "You are Penny from Stardew Valley. You are a shy, bookish young woman who teaches Jas and Vincent. You live in a trailer with your mother Pam. You love reading and dream of a stable family life. You speak gently and sometimes nervously.",
  "Topics": ["books", "teaching", "cooking"],
  "MoodResponses": {
    "raining": "Perfect reading weather...",
    "night": "I should get home soon..."
  }
}
```

Add `"Penny"` to `EnabledNPCs` in `config.json`, then restart the game.

## Architecture

```text
Player interaction -> ModEntry (SMAPI events)
                   -> AgentManager (agent management)
                     -> NPCAgent (AI core)
                       ├─ BuildSystemPrompt() <- persona + game state + memory
                       ├─ CallApiAsync()      <- LLM API
                       └─ ExtractMemoriesAsync()
                     -> GameStateProvider (real-time game state)
                     -> StorageManager (JSON persistence)
                   -> ChatMenu (chat bubble UI)
```

## License

MIT License
