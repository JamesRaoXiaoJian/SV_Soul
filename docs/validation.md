# 验证记录（2026-11-05）

## 不启动游戏的测试

- `dotnet run --project tests/Core/Core.csproj`：通过。直接编译生产 NPCAgent、配置、存储与人设代码；只替换游戏状态读取和日志接口，HTTP 使用模拟处理器。
- 覆盖 OpenAI URL、Bearer 认证、token 字段与文本解析；Anthropic endpoint、认证、system 字段与文本解析；失败请求不落盘；每十轮记忆提取失败后恢复上下文；历史保存、重新加载和裁剪。
- Mac 实际配置 `gpt-6.1-terra`：最小请求 HTTP 200，回复 OK。
- `scripts/check-conversation.py`：真实中文角色回复、多轮回忆“钓鱼/蓝莓47”、JSON 字符串数组记忆提取均通过。测试会产生少量 API 费用，不写玩家存档或记忆。
- Mac 自带 Python urllib 曾出现 TLS EOF；系统 curl 完成上述三项测试。此结果不等于已验证游戏内 .NET HttpClient 的联网路径。
- Release 构建通过，保留 CS9057 警告：SDK 6 编译器早于构建工具分析器要求，分析器未完整运行。
- 发布包检查通过：仅 DLL、manifest、人设 JSON；ZIP 权限已修正。

## 本轮修复

- 背景改为按矩形绘制，避免 drawDialogueBox 的内部定位与头像/内容坐标不一致。
- 中文欢迎语、发言标签、输入提示与关闭提示；思考状态固定在标题区。
- 记忆提取失败用 finally 恢复原对话；先裁剪再保存历史。
- 空密钥、空模型及无效 token/历史限制提前报错。

## 部署与待验证

新版 DLL 已原子替换到 Mac 的 Mods/SV_SOUL，旧 DLL 备份在 ~/RXJ/SV-Soul-tools/；未修改用户密钥或当前存档，未重启游戏。
需要重启后由用户走到 NPC 旁，验证头像/面板位置、中文输入法、滚动、缩放和真实游戏内 API 调用。当前不能宣称视觉验收通过。
