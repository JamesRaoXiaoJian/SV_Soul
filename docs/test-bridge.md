# 游戏内自动化测试入口

仅显式设置 `EnableTestBridge=true` 时启用；配置改变需重启游戏。默认关闭，不开放网络端口。

从 DSW 执行：

```bash
python3 scripts/mac-test.py open --npc Abigail
python3 scripts/mac-test.py send --text '你好，我喜欢钓鱼。'
python3 scripts/mac-test.py close
```

每条请求有独立 Id，以原子重命名写入 Mac 模组目录 `test-request.json`。主线程每 30 tick 检查一次，消费后删除；`test-result.json` 返回 Id、accepted/error、错误原因。accepted 只代表指令已执行或消息已提交，不代表 API 已成功响应；用聊天截图及日志确认最终结果。

仅在存档加载后执行；事件期间拒绝操作。open 传送玩家到 NPC 所在地图、NPC 下方一格，并打开聊天；地图碰撞与特殊 NPC 位置仍需截图验证。send 使用正常聊天提交路径，会调用真实 API 并写入本模组聊天历史。不会自动保存游戏。

此入口不处理自动选择/加载存档，也不让 C# DLL 热更新。首次启用或修改 DLL 仍需重启，然后加载测试存档。当前已编译部署，实际传送待重启后验证。

## 2026-11-06 实测

已重启并加载冬季第 7 日测试存档。open 请求 ab5d6301-e60b-40f4-9507-b8930b3f689d 返回 accepted；日志记录传送到 Abigail 所在 FarmHouse，截图确认聊天自动打开、头像与姓名位于面板内。
两次 send 请求均被消费，游戏使用 gpt-6.1-terra / OpenAI Chat Completions，但 HTTP 客户端均在 SSL 握手阶段失败。随后系统 curl 直接请求同一域名也报 SSL_ERROR_SYSCALL，网络问题尚未解决，不能认定真实回复闭环成功。
游戏存档未保存；重启前的本地存档备份：Mac ~/RXJ/SV-Soul-tools/save-backups/before-bridge-20261106。
