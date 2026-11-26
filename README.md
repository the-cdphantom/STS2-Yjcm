# 三国杀：一将成名

现已完成武诸葛亮、威董卓和威曹操三位角色。作者 CDphantom（B站：CD幻影），部分内容采用 AI 生成。Mod 交流群：1106930563。

这是公开的非商业源码仓库。作者署名 CDphantom；允许非商业修改、免费分享与模组联动，禁止商用和收费转载。具体条款见 [LICENSE](LICENSE)。这不是 MIT/GPL 或 OSI 认可的开源许可。

游戏成品仍通过 Slay the Spire 2 Steam 创意工坊免费发布；本仓库不发布成品，不提供图片、语音、动画或游戏 DLL。

## 工程

唯一业务工程是 `mod/Yjcm.csproj`，三位角色、事件和通用玩法共用一套源码。`GAME_BETA` 是沿用的 0.111+ API 宏，两版均启用。测试版同时启用 `GAME_V0113` 和 `GAME_V0114`。加载器位于 `loader`，遥测后端与仪表盘位于 `telemetry`。

固定游戏依赖为 stable 0.111.0 和 beta 0.114.0。构建工具包括 Godot 4.5.1 Mono、.NET SDK 10.0.203（目标框架 net9.0）、Node.js 和 PowerShell 7。

游戏 DLL 和素材不公开提交。开发者须合法获取匹配版本依赖，按 `dependencies` 的 manifest 准备文件，并在本地另行准备被忽略的运行素材；单独克隆本仓库不能完整导出或打包。详见 [素材准备说明](docs/资源准备.md) 与 [第三方声明](THIRD_PARTY_NOTICES.md)。

```powershell
node ./tools/generate_cards.mjs --write
node ./tools/generate_caocao_cards.mjs --write
pwsh ./tools/build.ps1 -Variant all
pwsh ./tools/package.ps1 -GodotPath 'C:/Tools/Godot_console.exe'
# 部署是独立显式步骤，仅向 mods/yjcm 写入，要求游戏关闭。
pwsh ./tools/deploy.ps1 -GamePath 'E:/SteamLibrary/steamapps/common/Slay the Spire 2'
```

本机可复制 `build.local.example.json` 为忽略的 `build.local.json` 配置路径。最终包为 `artifacts/dist/yjcm`，含加载器、两版 DLL、一份 PCK 及全部松散资源。所有产物均不入 Git。

## 修改约定

不要修改自动生成牌正文，先修改源卡与生成器，然后执行生成与检查。公开 API 类型、游戏模型 ID、存档字段、资源 res:// 路径和本地化键保持兼容。详见 `docs/interop`、`docs/结构方案.md` 与 `docs/迁移验收.md`。

公开 CI 仅做无游戏依赖的静态检查；真实编译/游戏验证在具备固定依赖和运行资源的本机执行。本机配置、token、玩家日志、游戏 DLL、缓存和备份严禁提交。

推送前运行 `node tools/check-public-source.mjs`，确认 Git 候选清单中没有素材、二进制、第三方实现快照和凭据。
