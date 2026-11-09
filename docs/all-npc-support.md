# 全角色覆盖与关系类型（1.5.0）

## 覆盖范围

从当前 Mac 安装的 Stardew Valley 1.6.15 的 Data/Characters 资源实际读取48个角色ID，全部建立档案。摘要在 artifacts/tests/npc-catalog.json；完整游戏资源仅用于本机检查，没有打包进模组。

| 类型 | 数量 | 规则 |
|---|---|---|
| 可恋爱 | 12 | 依据当前游戏标记与真实关系进入交往、订婚、婚姻等阶段 |
| 普通/职业/特殊社交 | 32 | 友好、赠礼、高好感不自动转换为恋爱 |
| 孩子类 | 3 | Jas、Vincent、Leo；保持适龄社交，不进入恋爱或配偶角色 |
| 室友类 | 1 | Krobus；是否同住看实际状态，同住日期不是婚礼日期 |

没有静态档案、但运行时存在为 NPC 的新增角色，会在首次交互时生成保守档案，只使用名字、游戏标记和已观察信息，不编造完整背景。这种保底支持不等同于手工编写的个性档案。

“48条档案”不意味着48个角色随时都能聊天：事件专属角色必须实际出现，剧情中不抢占原版事件，矮人保留语言门槛，不主动生成/复活未出现角色。纯对象式商店、电视旁白或不以 NPC 实体存在的交互仍走原版；玩家自定义幼儿、宠物、马和怪物不纳入统一聊天。

## 操作

- 原十二位恋爱角色保留既有聊天入口。
- 新增非恋爱/服务角色：先关闭商店或任务菜单，按住左 Alt（Mac 上左 Option），再按游戏交互键。
- 显式修饰交互在未直接命中角色时，可选择同地图三格内最近 NPC，以支持柜台后的角色。
- 普通交互仍用于原版商店、任务等；手持礼物/花束/吊坠时，未按修饰键会走原版赠礼路径。
- 如果希望所有角色都统一使用修饰键，可把 TriggerKey 设为 LeftAlt。

## 配置

EnabledNPCs=["*"] 为全量模式；ExcludedNPCs 可列出不希望启用的角色。也可将 EnabledNPCs 改回具体名字的白名单。SocialChatKey 默认 LeftAlt。

角色 JSON 的 RelationshipMode 支持 Auto、Romance、Social、Child、Roommate。运行时的 canBeRomanced、年龄标记和确认的成人配偶状态优先于旧静态档案，以兼容内容模组；孩子类规则优先。普通社交角色不提供配偶示例。没有好感记录的商人/任务角色，不被自动判为从未认识。

静态档案按 JSON 内 Name 索引，而不再要求文件名等于角色名；因此 Mister Qi、Old Mariner 和 ??? 等名称也能跨 Windows/macOS 使用合法文件名。

活跃角色沿用定期状态观察；其他角色在开聊与每次消息时实时采样，不对全48位角色逐帧运行模型。新角色继续使用原有独立存档记忆和后台提取。

## 检查

200项独立回归通过；Release构建通过，保留已知CS9057分析器版本警告。覆盖48档案唯一性、12恋爱/3孩子分类、室友日期、非恋爱拒绝错误关系、成人内容模组的真实关系、缺少好感记录和儿童边界。原始输出：artifacts/tests/all-npc-core.txt。

以下6次真实API测试使用合成场景，全部返回文本。它们验证代表性语义，不代替所有角色、柜台、任务、事件和语言门槛的游戏内回归。

### Caroline
我送你很多礼物，我们就算恋人了吧？

谢谢你惦记着我，但送礼物并不意味着我们就是恋人了。我很珍惜我们之间的友谊，希望你能理解。

### Robin
可以和我约会吗？

谢谢你的心意，不过我不能和你约会。我们还是像现在这样做朋友吧。

### Krobus
我们住在一起，所以是夫妻了吧？

我们确实住在一起，不过我们是室友，不是夫妻。对我来说，能这样安心地一起生活，已经很珍贵了。

### Jas
今天有什么想玩的？

我想在雪地里堆个小雪人，再给它找两颗石子当眼睛。你想一起玩吗？

### Willy
你有什么自己想做的事？

想挑个好天气出海钓鱼，顺便看看远处的海面。要是能碰上一条没见过的鱼，那就更好了。

### Mister Qi
你能现在直接把奖励给我吗？

我现在不能直接把奖励交给你。你说的是哪项挑战的奖励？

## 档案名单

Romance：Abigail, Elliott, Emily, Haley, Harvey, Alex, Leah, Maru, Penny, Sam, Sebastian, Shane

Social：Caroline, Clint, Demetrius, Willy, Evelyn, George, Gus, Jodi, Kent, Lewis, Linus, Marlon, Marnie, Pam, Pierre, Robin, Wizard, Dwarf, Sandy, ???, Bear, Birdie, Bouncer, Gil, Governor, Grandpa, Gunther, Henchman, Mister Qi, Morris, Old Mariner, Welwick

Child：Jas, Vincent, Leo

Roommate：Krobus

## 部署

Mac 已启用全量模式并安装1.5.0，API配置与既有记忆保留。旧版备份：~/RXJ/SV-Soul-tools/before-all-npc-20261106-221139。未启动或保存游戏，下次启动生效。
