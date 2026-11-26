# 一将成名开发约定

- 唯一业务工程为 mod/Yjcm.csproj；stable/beta 是游戏 API 构建目标，不是两套业务源码。加载器 loader 与 tests/InteropTests 是辅助工程。
- 3.0.0 起，默认正式包包含武诸葛亮、威董卓和威曹操。曹操源码和素材仍在原目录，正式构建编译该角色、合并其本地化与 PCK 资源，并附带 assets/experimental/WeiCaoCao 中的松散资源。
- 正式版使用 tools/build.ps1、package.ps1、deploy.ps1，可选的标记开发版保留 tools/build-caocao.ps1、package-caocao.ps1 和 deploy.ps1 -CaoCao。两条流程覆盖 stable/beta。E:/sts2mod/三国杀/yjcm 只接收正式包，本机游戏使用 mods/yjcm。
- Mod ID 精确为 Yjcm，内容程序集身份 Yjcm、加载器身份 Yjcm.Loader。新游戏模型 ID 使用 YJCM_*；不随目录调整修改已有 ID、存档字段或公开联动类型。
- 固定依赖使用 dependencies/stable-0.111.0 与 dependencies/beta-0.114.0，按 manifest.json 校验，不引用 Steam 当前 DLL 或 .godot 编译输出。
- 修改复制牌时先修改原卡、生成器或 tools/card-inputs，再运行 node tools/generate_cards.mjs --write；不得手改生成正文。生成器一套输出覆盖两 API 与共用本地化。
- 先运行 tools/build.ps1 -Variant all，再 tools/package.ps1。部署是另行明确步骤 tools/deploy.ps1；要求游戏关闭，不强制关闭游戏，不覆盖其他 Mod，仅部署 mods/yjcm。
- 用户已于 2026-10-19 授权：后续实现完成并验证/打包通过后，只要 SlayTheSpire2 未运行，就直接按上述脚本部署，不必再询问；游戏运行时停止在已打包状态并告知，不能强制关闭。此授权不包括 GitHub 提交或推送。
- 运行资源路径 res://Yjcm、res://WeiDongZhuo、res://images 与 res://scenes 保持兼容。松散资源的唯一来源 assets/loose，不能依赖旧项目或部署目录已有文件。
- artifacts、.godot、原版 DLL、玩家日志、存档、token、build.local.json 与 config.local.json 不提交。公开仓库 STS2-Yjcm 只含源码/脚本/本地化/文档，素材全部本地忽略。署名 CDphantom，按 LICENSE 允许非商业修改和联动，禁止商用与收费转载；不标成 MIT/GPL，不对第三方内容再授权。不自动新增成品发布渠道。
- 清理前核验准确目标、恢复方式与当前需求。不备份整个工作区，不删除其他 Mod、素材原件或工具安装目录；旧工程只保留工作区备份目录中的单份已校验 ZIP。
- 构建/审计通过不等于真实游戏/联机实测；未进游戏验证须明确说明。
