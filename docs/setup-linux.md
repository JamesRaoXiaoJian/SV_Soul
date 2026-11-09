# Linux 开发与游戏安装

2026-11-05 核查：SMAPI 最新稳定版 4.5.2，要求 Stardew Valley 1.6.14+；官方 PC 公告最新版本为 1.6.15。SMAPI 4.5.2 仍以 net6.0 为目标。构建工具 NuGet 最新稳定版为 4.4.0，项目已固定此版本。

来源：
- https://smapi.io/
- https://github.com/Pathoschild/SMAPI/releases/tag/4.5.2
- https://store.steampowered.com/news/app/413150
- https://github.com/Pathoschild/SMAPI/blob/4.5.2/src/SMAPI/SMAPI.csproj

## 安装游戏

在有桌面的 Linux 上安装 Steam，登录持有游戏的账号，在库中安装 Stardew Valley。使用默认原生 Linux 版本，不强制启用 Proton，也不要选择旧版 compatibility 分支。先启动原版游戏一次，再退出。

Steam 中右键游戏 → 管理 → 浏览本地文件，可获得实际安装目录。常见位置是 `~/.local/share/Steam/steamapps/common/Stardew Valley`，但以 Steam 显示路径为准。

也可使用购买账号下载 GOG 提供的 Linux 安装包。

DSW 通常没有桌面；准备程序集用于构建不等于完成游戏运行验证。真实交互验证可以在 Windows/macOS 游戏电脑上完成，无需为 DSW 专门部署远程桌面。

## 安装 SMAPI

从 SMAPI 官网下载所需版本，可解压到项目 `tools/smapi/`（本机依赖，不提交到 Git）。解压目录中提供 Linux、macOS 和 Windows 安装入口。

Linux 安装：

```bash
bash 'tools/smapi/SMAPI 4.5.2 installer/install on Linux.sh'
```

按照交互提示指定真实游戏目录。SMAPI 运行文件必须安装到游戏目录，不能在没有游戏文件的源码目录中独立运行。源码仓库不包含游戏文件；使用你自己的游戏安装目录完成安装。

macOS 请按 https://stardewvalleywiki.com/Modding:Installing_SMAPI_on_Mac 完成系统安全授权；Windows 使用包内 `install on Windows.bat`，根据安装器提示设置 Steam 启动项。

## 构建与兼容性

```bash
source scripts/env.sh
dotnet build -c Release -p:GamePath='/实际游戏目录' -p:EnableModDeploy=false
```

使用已经安装 SMAPI 的游戏目录。把构建产物和 `manifest.json`、`assets` 放入游戏 `Mods/SV_SOUL/`。仅使用官方构建工具生成的发布包，不将游戏 DLL、SMAPI 安装包或 API 密钥打包发布。

当前保留 `MinimumApiVersion=4.0.0`：这是最低 API 要求，不是项目安装的 SMAPI 版本；仅安装新版不构成提高最低版本的理由。
尚未完成对 4.5.2 / 1.6.15 的编译和游戏内兼容验证，不能据此声称完全兼容。待游戏可用后验证：NPC 开关聊天、中文输入、缩放与滚动、两种 API、保存与重进游戏。Windows/macOS 分别做游戏内测试。

## macOS 部署记录

Mac 已安装 SMAPI 4.5.2，游戏为 1.6.15。本次构建包在 macOS 解压后曾出现权限位为 000，导致 SMAPI 无法读取 manifest。部署后需确保 Mod 目录可读：`chmod -R u+rwX,go+rX /实际路径/Mods/SV_SOUL`。屏幕录制与辅助功能需在系统设置中分别授权；SSH 登录权限不包含它们。

发布前执行 `python3 scripts/fix-package-permissions.py 'bin/Release/net6.0/SV_SOUL 1.0.0.zip'`，修正 ZIP 权限，避免用户解压后手动 chmod。此修正须在每次构建后执行。
