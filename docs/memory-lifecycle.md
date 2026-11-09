# 记忆 v2 数据与生命周期

## 身份及存储

`SaveMemory` 保存 schemaVersion、SaveId、PlayerId，以及以 NPC 内部 ID 分组的对话和事实。正式数据通过 SMAPI `memory-v2` save-data 写入；只支持单人/主机玩家。切换存档会取消会话请求、丢弃旧运行时对象；旧任务只能持有已脱离当前游戏的存储实例。

存档保存时提交快照。退出不保存或加载旧存档时恢复相应记忆；检索索引只在 RAM 中构建。复制完整存档可携带记忆，Steam 云同步成功与否由平台决定。新版本不读取旧版按 NPC 命名的 JSON。

显式迁移：加载目标存档后，在 SMAPI 控制台执行 `svsoul_import_legacy Abigail`。导入旧 KeyFacts 为 LegacyUnverified，时间 unknown，保留原文件，正常保存游戏后才提交。未知来源数据不自动推断为任何游戏事件。

## 人设和时间

CoreTraits、Voice 为稳定个性，HistoricalBackground 只表示历史背景；StageProfiles 提供 Married、Dating、Engaged、Divorced 的表达规则。旧 SystemPrompt 作为第三方角色文件兼容输入。游戏快照优先于陈旧背景；已婚不等于已知完整婚礼经历。

主线程读取游戏日期、时刻、地区降雨、关系状态、友好度与心数、婚礼日期（缺失 unknown）、配偶、共同家庭、生日、节日和地点。NPC 当前移动状态可观察，但不臆造正在进行的活动目的。未婚角色当前所在地图不自动作为住所。

MemoryRecord 有稳定 Id、Kind、Key、Text、GameTime、RecordedAt、EvidenceIds、Active、SupersededBy、ValidUntil、Importance。GameTime 是观察/陈述时间，初次观察到已婚不意味着当天结婚。真实婚礼日期单独来自快照。现实 UTC 时间用于存储排序/诊断，角色的今天/昨天按游戏日历计算。

## 来源及更新

GameFact：受保护的关系/住所等状态；变化时旧记录失效而不被覆盖。
GameEvent：成功赠礼的事件证据；通过 receiveGift 前后计数确认，补丁不匹配时明确报日志而不虚构。
PlayerStatement：玩家明确说出的持久偏好/计划；同主题更正替代当前旧说法，保留历史。
Interpretation：对玩家的暂时理解，至少引用两个不同游戏日的玩家证据；不会修改 NPC 核心性格或游戏好感度。
LegacyUnverified：显式导入但不能验证所属经历的旧数据。

模型提取须引用真实玩家消息 Id；助手说“送了礼物”不会成为 GameEvent。当前游戏事实始终进入系统提示，其他记忆按来源和日期标注为数据。

## 快慢路径

发送：主线程快照 → 后台检索/组装 → 一次 HTTP 回复 → 对应窗口响应队列。
整理：每十个成功轮次或空闲30秒，最多20条消息一批，全局串行抽取；后台与回复分离。游戏退出会取消请求。未完成批次的 ExtractedThrough 随存档保存；失败不破坏历史，待后续对话或重载继续处理。

近期提示最多8轮，历史文本预算约3000字符；单次玩家输入最多1000字符；系统与历史总预算8000字符，优先保留权威快照、关系阶段和语言规则；记忆段最多1800字符。

检索：范围由存储实例隔离；当前事实通常只取 Active。历史问法允许失效记录，日期问法支持今天/昨天/前天。Lucene BM25 + 中文双字分析 + 少量中英文别名，结合近期性/重要性的倒数排名融合，最多8条。首次建索引和热检索分别计时；embedding、远程重排默认不存在。

## 调试

`python3 scripts/mac-test.py inspect --npc Abigail` 导出 Mac Mod 目录 test-context.json，包含当前世界快照及记忆，无密钥。open/send/close 保留。调试导出是诊断数据，不能被重新注入为记忆。测试不自动保存游戏。
