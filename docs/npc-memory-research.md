# NPC 动态人设与记忆：调研及决策

调研基准：2026-11-06。目标：Windows/macOS SMAPI 模组、中文交互、明确的游戏事实、低延迟，无额外常驻服务。

## 决策

选择方案 A：游戏专用的 C# 认知层，SMAPI save-data 为权威存储，Lucene.NET 为可重建检索索引。游戏内每轮对话只请求一次回复模型；抽取与角色理解在后台完成。保留现有 OpenAI/Anthropic 请求接口。

| 路线 | 收益 | 代价 | 决策 |
|---|---|---|---|
| A：C# + SMAPI + Lucene.NET | 与存档生命周期一致，无额外进程，纯托管跨平台检索 | 需实现游戏来源、时间、知情范围 | 默认采用 |
| B：腾讯独立 MemoryCore | 复用分层流水线、中文检索、抽取和聚合 | Node 服务与跨平台部署、游戏回档适配仍需实现 | 未来组件级备选 |
| C：Mem0/Hindsight | 通用记忆与复杂召回能力丰富 | 运行依赖和模型调用成本较高 | 当前不引入 |
| D：Letta/LangGraph 接管 Agent | 适合工具执行和自主规划 | 超出 NPC 聊天范围 | 当前不引入 |

## 项目调查

- [TencentDB-Agent-Memory](https://github.com/TencentCloud/TencentDB-Agent-Memory)：MIT；本轮核对提交 `2002196f2d7c22ea267cfcaf67c5a6fa861df681`。L0 原文、L1 原子事实、L2 场景、L3 画像；独立 [MemoryCore](https://github.com/TencentCloud/TencentDB-Agent-Memory/blob/feat/server_team/MemoryCore/README.md) 使用 Node >=22.16、SQLite、本地文件。BM25 可不依赖 embedding；须检查 FTS5。中文采用 jieba 预分词。完整 Hub/Proxy/Knowledge 并非必须安装。其 L3 主要面向用户画像与团队原则，不能直接当成 NPC 自我人格。借鉴分层、异步、预算与来源，不移植团队管理功能。
- [Mem0](https://github.com/mem0ai/mem0)：Apache-2.0；当前 OSS 是 ADD-only 单次抽取、混合召回与实体关联。旧版 UPDATE/DELETE、图记忆教程不能直接套用；图记忆与部分时间能力属于 Platform。参考 [迁移说明](https://github.com/mem0ai/mem0/blob/main/docs/migration/oss-v2-to-v3.mdx)。适合通用玩家画像，不能代替游戏权威事实与回档规则。
- [Hindsight](https://github.com/vectorize-io/hindsight)：MIT；retain/recall/reflect，区分事实、经历、综合观察与理解，有证据关联。支持嵌入式 PostgreSQL，并非必须部署外部数据库，但仍有完整检索、向量、重排、后台处理体系。借鉴事实与主观理解分离。
- [Graphiti](https://github.com/getzep/graphiti)：Apache-2.0；事实有效区间、失效历史、来源 episode。支持图数据库及嵌入式选项。借鉴时间有效性，不引入图数据库。
- [Letta](https://github.com/letta-ai/letta)：Apache-2.0；活跃代码已迁往 [letta-code](https://github.com/letta-ai/letta-code)，旧 V1 服务端归档。核心记忆块、长期身份和后台整理有参考价值，完整 Agent harness 与 git 记忆对本项目过重。
- [LangMem](https://github.com/langchain-ai/langmem)：MIT；功能核心独立于存储，profile 与 collection、后台更新值得借鉴。无需为 C# 模组增加 Python。
- [Generative Agents](https://arxiv.org/html/2304.03442v2)：参考相关性、近期性、重要性召回和经历反思；论文的自主规划循环不纳入本项目。反思只能成为带证据的推断，不能改写世界事实。

以上是源码/官方文档调研，不是这些框架在本机的性能对比实验。厂商 benchmark 不作为本模组验收结果。

## 复用边界

直接复用 `Lucene.Net`、`Lucene.Net.Analysis.Common` 4.8.0-beta00018（支持 net6.0，固定 Beta 版本），BM25 与 CJKAnalyzer 来自 Apache 项目；不自写倒排索引或分词器。J2N 与 Microsoft.Extensions 作为传递依赖随包发布。许可位于 assets/licenses。

自定义代码仅承担游戏时间、存档隔离、NPC 知情边界、来源校验、关系转换、提示词组装及队列调度。无需新部署 SQLite、Node/Python 服务、向量数据库或另一个推理模型。

## 已落地接口

`WorldContextSnapshot` 承载只读世界快照和采集耗时；`IMemoryRepository` 按存档/玩家实例隔离；`IMemoryRetriever` 负责有限召回；`MemoryRecord` 记录来源、游戏日期、稳定证据 ID 与失效关系。NPC 核心不再依赖 Game1。

当前历史查询支持“以前/曾经/过去/当时/结婚前”和英文对应提示；日期查询覆盖今天/昨天/前天。任意自然语言时间推理、通用语义向量召回、自动自主行动和联机农场工人同步不在此版本中。
