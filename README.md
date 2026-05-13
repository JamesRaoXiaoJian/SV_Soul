# SV_SOUL

AI-Powered Chat NPCs for Stardew Valley.让游戏中的 NPC 拥有真正的 AI 对话能力 -- 专属人设、持久记忆、实时环境感知。

## 特性

- **独立智能体** -- 每个 NPC 是独立的 AI 实例，拥有专属性格、说话风格和记忆
- **实时感知** -- NPC 知道当前季节、天气、时间、地点和友好度，并据此调整对话
- **持久记忆** -- NPC 记住你说过的话和一起经历的事，重启游戏后不丢失
- **原生风格 UI** -- 全屏聊天气泡界面，头像、滚动、"正在思考"动画一应俱全
- **五个内置角色** -- Abigail、Shane、Sebastian、Elliott、Haley，各有完整人设
- **易于扩展** -- 添加 JSON 文件即可让任意 NPC 具备 AI 对话能力

## 分支说明

| 分支 | 用途 | 内容 |
|------|------|------|
| `main` | 源代码 | 完整项目源码，开发者自行构建 |
| `release` | 发布版 | 构建好的 mod 文件，玩家下载即用 |

## 玩家安装（release 分支）

1. 安装 [SMAPI](https://smapi.io/) 4.0.0+
2. 从 `release` 分支下载 `SV_SOUL` 文件夹
3. 将 `SV_SOUL` 文件夹放入 `Stardew Valley/Mods/`
4. 启动游戏让 SMAPI 生成配置文件
5. 编辑 `Stardew Valley/Mods/SV_SOUL/config.json`，填入 API Key：

```json
{
  "ApiKey": "你的API密钥",
  "Model": "claude-sonnet-4-20250514"
}
```

6. 进入游戏，靠近 NPC 按交互键即可开始聊天

## 开发者构建（main 分支）

### 环境要求

- .NET 6.0 SDK
- Stardew Valley 1.6+（用于自动引用游戏程序集）

### 构建步骤

```bash
git clone https://github.com/JamesRaoXiaoJian/SV_Soul.git
cd SV_Soul
dotnet build
```

构建完成后：
- DLL 输出到 `bin/Debug/net6.0/SV_SOUL.dll`
- 自动部署到 `Stardew Valley/Mods/SV_SOUL/`
- Release zip 生成在 `bin/Debug/net6.0/`

### 项目结构

```
SV_Soul/
├── ModEntry.cs                  # SMAPI 入口，事件监听
├── SV_SOUL.csproj               # 项目文件
├── SV_SOUL.sln                  # 解决方案
├── manifest.json                # SMAPI mod 元数据
├── Agents/
│   ├── AgentManager.cs          # 智能体生命周期管理
│   ├── NPCAgent.cs              # 单个 NPC 的 AI 核心（提示构建、API 调用、记忆提取）
│   └── GameStateProvider.cs     # 读取实时游戏状态
├── Config/
│   └── ModConfig.cs             # 配置模型
├── Models/
│   └── NPCPersonality.cs        # NPC 人设数据结构
├── Storage/
│   ├── StorageManager.cs        # JSON 文件持久化
│   ├── NPCMemory.cs             # 记忆数据结构
│   └── ConversationTurn.cs      # 对话轮次数据结构
├── UI/
│   ├── ChatMenu.cs              # 全屏聊天气泡界面
│   └── TextInputBox.cs          # 文本输入组件
└── assets/
    └── personalities/           # NPC 人设 JSON
        ├── abigail.json
        ├── shane.json
        ├── sebastian.json
        ├── elliott.json
        └── haley.json
```

## 配置项

编辑 `config.json`（首次运行后自动生成）：

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `ApiKey` | API 密钥 | `""` |
| `BaseUrl` | API 地址，`null` 为官方地址 | `null` |
| `EnabledNPCs` | 启用 AI 对话的 NPC 列表 | 全部 5 个 |
| `MaxTokens` | 单次回复最大 token 数 | `200` |
| `MaxHistoryTurns` | 保留的最大对话轮数 | `50` |
| `Model` | 模型标识符 | `mimo-v2.5-pro` |
| `TriggerKey` | 触发对话需按住的键，`null` 为直接交互 | `null` |

### 使用代理 API

```json
{
  "BaseUrl": "https://your-proxy.example.com/anthropic",
  "Model": "your-model-name"
}
```

API 调用遵循 Anthropic Messages API 格式，endpoint 为 `{BaseUrl}/v1/messages`。

## 使用方法

1. 靠近支持的 NPC（Abigail、Shane、Sebastian、Elliott、Haley）
2. 按交互键（默认空格/鼠标右键）
3. 在聊天界面输入文字，按 Enter 发送
4. 按 Esc 关闭界面

| 操作 | 按键 |
|------|------|
| 发送消息 | Enter |
| 关闭聊天 | Esc |
| 滚动记录 | 鼠标滚轮 |
| 输入框聚焦 | 鼠标点击 |

## 添加自定义 NPC

在 `assets/personalities/` 下创建 JSON 文件：

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

在 `config.json` 的 `EnabledNPCs` 中添加 `"Penny"`，重启游戏即可。

## 架构

```
玩家交互 → ModEntry (SMAPI 事件)
         → AgentManager (智能体管理)
           → NPCAgent (AI 核心)
             ├─ BuildSystemPrompt() ← 人设 + 游戏状态 + 记忆
             ├─ CallApiAsync()      ← 调用 LLM API
             └─ ExtractMemoriesAsync() ← 每 10 轮提取记忆
           → GameStateProvider (实时状态)
           → StorageManager (JSON 持久化)
         → ChatMenu (聊天气泡 UI)
```

## 许可证

MIT License
