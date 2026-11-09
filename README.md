# SV_SOUL

[中文](README.md) · [English](README_EN.md)

星露谷物语 AI NPC 模组。NPC 根据自己的性格、与玩家的关系、已确认的记忆和当前游戏状态对话，并按需检索游戏背景。

当前源码版本：**1.7.0**。目标游戏为 Stardew Valley 1.6，已使用 1.6.15 / SMAPI 4.5.2 的程序集验证构建。

## 功能

- **48 个角色档案**：区分可恋爱角色、普通社交角色、孩子和室友，保留独立语气、兴趣和关系阶段。
- **存档记忆**：对话和记忆按存档、玩家、NPC 隔离，随游戏保存；不保存退出时同步回滚。目前支持单人及多人游戏的主机玩家。
- **实时场景**：关系、住所、天气、游戏时间、生日和节日参与提示词，当前事实优先于旧背景。
- **背景检索**：107 条随包资料，加上游戏资源中的生日、亲友、节日和常见物品说明。原版 1.6.15 的离线样本共 509 条；内容模组可改变运行时数量。
- **轻量检索**：本地 Lucene.NET，按知情角色与剧情条件筛选，每轮最多 3 条、1,000 字符；后台建库，无额外模型调用。
- **历史回看与候选回复**：重开窗口恢复消息，支持分页、请求期间关闭重开，以及无需打字的候选交互。
- **双 API 格式**：支持 OpenAI 兼容 Chat Completions 和 Anthropic Messages，聊天与记忆整理共用配置。

## 安装与配置

1. 安装游戏和 [SMAPI](https://smapi.io/)。本项目的验证组合为游戏 1.6.15 / SMAPI 4.5.2。
2. 获取与目标版本一致的构建包，或按下方步骤从 `main` 构建。`release` 是独立分支，版本以其中的 `manifest.json` 为准。
3. 退出游戏，将包中的 `SV_SOUL` 文件夹放入游戏的 `Mods/` 目录。
4. 将 [config.openai.example.json](config.openai.example.json) 复制为 `Mods/SV_SOUL/config.json`，填入自己的 API 密钥、服务地址和模型标识符。
5. 通过 SMAPI 启动游戏。

OpenAI 兼容配置示例：

```json
{
  "ApiProvider": "OpenAI",
  "ApiKey": "YOUR_API_KEY",
  "BaseUrl": "https://api.openai.com/v1",
  "Model": "YOUR_MODEL_ID",
  "UseMaxCompletionTokens": true,
  "SendTemperature": true,
  "MaxTokens": 200,
  "Language": "zh"
}
```

模型需由所用服务提供。`BaseUrl` 支持服务域名、`/v1` 前缀或完整 `/chat/completions` 地址；其他版本前缀请填写完整 endpoint。只接受 `max_tokens` 的服务设 `UseMaxCompletionTokens=false`，不接受温度参数的模型设 `SendTemperature=false`。

Anthropic 模式设 `ApiProvider="Anthropic"`，填写相应模型；`BaseUrl=null` 使用默认服务地址。为兼容旧配置，代码默认 provider 仍为 Anthropic。实际消息会发送给配置的 API 服务。

更新模组时保留自己的 `config.json` 和游戏存档，退出游戏后替换文件。API 密钥、玩家数据和游戏文件不应提交到 Git。

## 操作

普通社交／服务 NPC：按住 **左 Alt（Mac 为 Option）再交互**打开 AI 对话，正常交互保留商店和任务。可恋爱角色按角色配置使用正常交互。手柄默认用 **Y** 与附近 NPC 开聊。剧情、语言和出场条件仍由游戏决定。

| 操作 | 键鼠 | 手柄 |
|---|---|---|
| 发送输入 | Enter | X 发送已有输入 |
| 选择候选回复 | 点击；Tab 切换焦点后用方向键 | 方向键／左摇杆 |
| 发送候选回复 | Enter／点击 | A |
| 下一页候选 | 候选区 Space／换一组按钮 | Y |
| 滚动记录 | 鼠标滚轮 | LB/RB 或右摇杆上下 |
| 关闭对话 | Esc | B |

`ControllerChatButton` 可修改开聊按键。候选回复在本地生成，不增加 API 请求。

## 常用配置

| 配置 | 默认值 | 用途 |
|---|---|---|
| `EnabledNPCs` | `["*"]` | 全量模式，或填写角色内部 ID 白名单 |
| `ExcludedNPCs` | `[]` | 排除角色 |
| `SocialChatKey` | `LeftAlt` | 社交角色 AI 交互修饰键 |
| `ControllerChatButton` | `ControllerY` | 手柄开聊键 |
| `TriggerKey` | `null` | 可选的额外交互修饰键 |
| `MaxHistoryTurns` | `8` | 模型近期对话轮数上限，与界面历史分页独立 |
| `MaxTokens` | `200` | 回复 token 上限 |
| `Temperature` | `0.9` | 在服务支持时发送 |
| `Language` | `zh` | 默认回复语言 |
| `EnableTestBridge` | `false` | 开发用游戏内测试桥 |
| `DebugMode` | `false` | 调试日志 |

完整字段见 [ModConfig.cs](Config/ModConfig.cs)。旧版未归属存档的记忆迁移见 [记忆生命周期](docs/memory-lifecycle.md)。

## 开发与验证

需要 .NET 6 SDK，以及已安装 SMAPI 的合法游戏程序集。

```bash
git clone https://github.com/JamesRaoXiaoJian/SV_Soul.git
cd SV_Soul
dotnet build -c Release -p:GamePath="/path/to/Stardew Valley" -p:EnableModDeploy=false
```

输出目录为 `bin/Release/net6.0/`，包含 DLL 和版本化安装 ZIP。`EnableModDeploy=false` 可避免构建时修改游戏安装。Linux 环境说明见 [setup-linux.md](docs/setup-linux.md)。

不启动游戏即可执行：

```bash
dotnet run --project tests/Core
python3 scripts/validate-lore-corpus.py
```

`--native-lore` 扩展测试还需要本机授权提取的游戏资源，详见 [背景库说明](docs/history-and-lore.md)。API 检查脚本使用你提供的本地配置，会产生 API 费用。开发测试桥可操作游戏，仅在需要时显式启用。

1.7.0 在 Linux、Windows 和 macOS ARM64 上分别通过 **467 项离线检查、91/91 条标注检索问题**。万条合成资料预热检索 P95 分别为 0.190、0.066、0.088 ms；这是检索耗时，不是模型回复耗时。本轮未启动游戏或验证界面，完整范围见 [验收记录](docs/lore-validation.md)。

## 仓库结构

| 目录 | 内容 |
|---|---|
| `Agents/` | 会话、API 调用和主线程游戏状态采集 |
| `Models/` | 人设、关系边界、语气与候选回复 |
| `Storage/` | 存档记忆、背景资料与检索索引 |
| `UI/` | 聊天窗口、输入和手柄交互 |
| `assets/personalities/` | 角色档案 |
| `assets/lore/` | 有来源与知情条件的背景资料 |
| `tests/` | 独立测试及标注查询 |
| `scripts/` | 验证、打包和远程测试脚本 |
| `docs/` | 设计、资料维护和版本验证记录 |

`game/`、`tools/`、构建缓存及大部分 `artifacts/` 为本机工作目录，保留在本地。仓库仅收录可公开的背景检索验收记录。文档导航见 [docs/README.md](docs/README.md)。

## 许可证

代码采用 [MIT](LICENSE)。Wiki 衍生背景资料采用独立的 **CC BY-NC-SA 3.0**，具体出处与要求见 [背景资料许可](assets/lore/NOTICE.md)。第三方组件声明位于 [assets/licenses/](assets/licenses/)。
