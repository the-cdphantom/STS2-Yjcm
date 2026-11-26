# 一将成名遥测：部署与验收教程

本目录存放一将成名的 Cloudflare Worker、本地仪表盘与测试；游戏内客户端统一位于 `mod/src/Telemetry`。申请方 ID 精确为 `Yjcm`，覆盖武诸葛亮、威董卓与威曹操，不读取旧模组 ID 的历史数据。个人配置与登录缓存被 Git 忽略。

本地查看数据可双击 `E:/sts2mod/打开遥测仪表盘.cmd`，或本目录的 `打开遥测仪表盘.cmd`。
服务地址为 `http://127.0.0.1:4173/`。项目已归并到 `E:/sts2mod/Yjcm/telemetry`，下文旧“武诸葛亮遥测”目录不再使用。

## 你需要亲自完成的事情

只有账号和密钥相关的步骤必须由你操作：

1. 注册 PostHog，创建项目并复制 **Project token**。
2. 注册 Cloudflare，并在本目录运行登录、部署和 Secret 命令。
3. 把部署后得到的 Worker 根地址填入 Mod，或把该地址发给 Codex 代为填写和重新构建。
4. 进游戏同意需要的遥测类别，再到 PostHog 检查事件。

不要把 Project token 发到聊天、截图、源码或 `wrangler.jsonc`。输入令牌时只粘贴到 `npm.cmd run secret:put` 的终端提示中。

## 当前代码状态

- Mod 端已接好 RitsuLib 授权系统，由唯一工程分别构建 stable 0.111.0 与 beta 0.113.0 两个 API 目标。
- 同一申请方覆盖武诸葛亮、威董卓与威曹操，不新增第二个授权弹窗；跑局通过原版 `run_character_ids` 在本地仪表盘中分角色统计。
- 双版本 Loader 会单独调用遥测注册入口，不依赖 `MainFile.Initialize`。
- Cloudflare Worker 已部署并通过 `/health` 检查，Mod 端使用根地址 `https://wuzhugeliang-telemetry.cdphantom-mod.workers.dev`；是否发送各类数据仍完全取决于玩家在 RitsuLib 中的逐项授权。
- Worker 只接受当前申请方 `Yjcm` 的四种白名单事件，并拒绝错误 schema、类别、事件名、过大批次和非 JSON 请求；旧申请方 `WuZhugeliang` 的历史数据不再兼容或读取。
- Worker 会移除事件属性中的 `$ip`，不主动把玩家 IP 转发给 PostHog。Cloudflare 作为网络服务商仍会按其平台规则处理网络请求。

## 会收集什么

RitsuLib 会逐类展示用途并征得玩家授权。拒绝或撤销某类授权后，该类发送调用会变成无操作。

1. **基础使用信息**：游戏、Mod、RitsuLib 版本，平台、语言和匿名安装 ID。
2. **Mod 清单**：Mod ID、版本、加载状态与玩法影响标记，不含本地文件路径。
3. **诊断信息**：异常类型、RitsuLib 脱敏后的消息与堆栈、框架运行状态。
4. **跑局历史**：仅非主动放弃跑局的原版 run-history JSON，用于平衡分析。

## 第一步：创建 PostHog 项目

1. 根据数据存放区域，打开 [PostHog 美国区](https://us.posthog.com/signup) 或 [PostHog 欧盟区](https://eu.posthog.com/signup) 注册。
2. 新建项目，例如命名为 `WuZhugeliang Telemetry`。
3. 进入 **Settings → Project → Project variables**（界面也可能显示为 Project settings）。
4. 复制 **Project token**，一般以 `phc_` 开头。不要复制 Personal API key、Project ID 或 Secret API key。
5. 记住你选择的是美国区还是欧盟区，下一步的转发地址必须匹配。

PostHog 的 `/capture/`、`/batch/` 数据摄取使用 Project token；它和用于管理后台的 Personal API key 不是同一种密钥。

## 第二步：确认 PostHog 区域

打开 `wrangler.jsonc`：

```jsonc
"POSTHOG_HOST": "https://us.i.posthog.com"
```

- 美国区保持 `https://us.i.posthog.com`。
- 欧盟区改为 `https://eu.i.posthog.com`。

只改这一处普通变量；Project token 不得写进该文件。

## 第三步：本地检查

打开 PowerShell，逐行运行：

```powershell
Set-Location -LiteralPath 'E:\sts2mod\武诸葛亮遥测'
$env:npm_config_cache = 'E:\sts2mod\武诸葛亮遥测\.npm-cache'
npm.cmd ci
npm.cmd run check
npm.cmd test
npm.cmd run deploy:dry
```

预期结果：

- `npm.cmd ci` 从锁文件安装依赖到本目录的 `node_modules`。
- `npm.cmd run check` 无语法错误。
- `npm.cmd test` 全部测试通过。
- `npm.cmd run deploy:dry` 能生成 Worker 包，但不会正式上线。

`.npm-cache`、`node_modules`、`.config` 和 `.wrangler` 都留在本遥测目录，并已写入 `.gitignore`。

## 第四步：登录 Cloudflare

1. 先在浏览器注册或登录 [Cloudflare](https://dash.cloudflare.com/sign-up)。
2. 回到同一个 PowerShell 窗口运行：

```powershell
npm.cmd run login
```

3. 浏览器会打开 Cloudflare OAuth 页面；选择你的账号并允许 Wrangler 管理 Workers。
4. 终端显示登录成功后返回。登录配置被本项目脚本定向到 `E:\sts2mod\武诸葛亮遥测\.config`，不会落到项目根目录。

如果浏览器回调失败，可在本目录直接运行：

```powershell
npx.cmd wrangler login --device
```

再按终端给出的地址和验证码授权。

## 第五步：创建 Worker 并保存令牌

先进行第一次部署，以创建名为 `wuzhugeliang-telemetry` 的 Worker：

```powershell
npm.cmd run deploy
```

终端会显示类似下面的公开根地址：

```text
https://wuzhugeliang-telemetry.<你的子域>.workers.dev
```

此时客户端尚未接入，所以短暂没有 PostHog Secret 不会泄漏或丢失正式数据。接着运行：

```powershell
npm.cmd run secret:put
```

看到 `Enter a secret value:` 后，粘贴刚才的 **PostHog Project token** 并回车。终端不会回显字符，这是正常现象。该值以名为 `POSTHOG_API_KEY` 的 Cloudflare Secret 保存，不会写入仓库文件。

确认 Secret 名称存在：

```powershell
npm.cmd run secret:list
```

输出里应有 `POSTHOG_API_KEY`；列表只显示名称，不显示令牌值。最后再部署一次当前代码：

```powershell
npm.cmd run deploy
```

Cloudflare 官方说明 `wrangler secret put` 本身会创建并部署一个新版本；最后一次 `deploy` 是为了明确保证线上代码与当前目录一致。

## 第六步：检查 Worker

把你的根地址加上 `/health` 后用浏览器访问，例如：

```text
https://wuzhugeliang-telemetry.<你的子域>.workers.dev/health
```

应返回：

```json
{"ok":true,"service":"wuzhugeliang-telemetry"}
```

注意：填进 Mod 的是 **Worker 根地址**，不要添加 `/health`、`/batch` 或末尾斜杠。RitsuLib 会自动请求 `/batch/`。

## 第七步：把 Worker 地址接入 Mod

打开 `mod/WuZhugeliangTelemetry.cs`，找到：

```csharp
private const string WorkerHost = "";
```

改成实际根地址：

```csharp
private const string WorkerHost = "https://wuzhugeliang-telemetry.<你的子域>.workers.dev";
```

推荐直接把根地址发给 Codex，让 Codex 完成双版本源码检查、版本递增、构建和部署；**不要把 Project token 一起发来**。

如果自行处理，至少要重新构建当前游戏版本的 Mod DLL 与双版本 Loader，并按项目部署规则复制 DLL。Steam `mods` 目录仍然不能放 `WuZhugeliang.json`，否则会产生重复 Mod ID。

## 第八步：游戏内授权与验收

1. 完全退出并重启游戏，确保新 DLL 已加载。
2. 进入 RitsuLib 的 Mod 设置/遥测授权界面。
3. 逐项阅读并允许你愿意提供的类别；玩家可以随时拒绝或撤销。
4. 开始一局游戏，以产生基础使用和 Mod 清单事件。
5. 正常结束一局，以产生 `run_history.completed`；主动放弃不会上传跑局历史。
6. 回到 PostHog，打开 **Activity / Events** 页面等待新事件出现。PostHog 的活动页通常按固定间隔刷新，不一定瞬时显示。

本项目允许的事件名只有：

```text
session_start
mod_inventory
exception
run_history.completed
```

若这四种之外的事件到达 Worker，会被拒绝。

## 常见问题排查

### `/health` 打不开

- 再运行 `npm.cmd run deploy`，使用命令最后输出的最新地址。
- 登录 Cloudflare 控制台，检查 Workers & Pages 中是否存在 `wuzhugeliang-telemetry`。
- 确认测试的是 `/health`，不是 PostHog 地址。

### `/health` 正常，但 PostHog 没事件

按顺序检查：

1. `WorkerHost` 是否仍为空，或误带 `/health`、`/batch`。
2. `npm.cmd run secret:list` 是否包含 `POSTHOG_API_KEY`。
3. PostHog 项目是否与 Project token 属于同一个项目。
4. `POSTHOG_HOST` 是否选对美国区/欧盟区。
5. 游戏里是否同意了相应遥测类别。
6. 是否已重新构建、部署 DLL 并重启游戏。
7. 查看游戏日志 `C:\Users\CDphantom\AppData\Roaming\SlayTheSpire2\logs\godot.log` 中带有 `遥测`、`Telemetry` 或 `WuZhugeliang` 的行。

### Secret 填错

重新运行即可覆盖旧值：

```powershell
npm.cmd run secret:put
npm.cmd run deploy
```

不要尝试把 Secret 读取出来；Cloudflare 只允许覆盖或删除，不显示原值。

### 要暂时停用遥测

把 `WorkerHost` 改回空字符串并重新构建 Mod。这样不会注册遥测申请方，也不会弹授权提示或发起请求。无需删除 Worker 或 PostHog 项目。

## 本地 Worker 联调（可选）

只有需要调试 Worker 时才做。新建已被 `.gitignore` 排除的 `.dev.vars`：

```text
POSTHOG_API_KEY=仅供本地测试的项目令牌
```

然后运行：

```powershell
npm.cmd run dev
```

`.dev.vars` 只能留在本机，不要部署、提交、截图或发送。Cloudflare 官方也建议本地 Secret 使用 `.dev.vars`/`.env`，线上 Secret 使用 `wrangler secret put`。

## 上线后的安全事项

- Project token 只存在于 PostHog 后台与 Cloudflare Secret 中。
- Worker 地址是公开的，不能把它当作密钥；代码中的白名单和体积限制用于降低滥用风险。
- 建议在 Cloudflare 控制台为 Worker 增加速率限制，并定期查看请求量和异常状态码。
- 对外发布时应在 Mod 说明中列出收集类别、用途、处理服务（Cloudflare/PostHog）及关闭方法。
- 修改收集范围或用途后，必须同步更新玩家可见的授权说明，不能沿用旧同意。

## 本地可视化仪表盘

项目内已经提供独立的 `dashboard` 本地网页。它会从 PostHog 读取现有的、已经过玩家授权的事件，并可在“全部角色／武诸葛亮／威董卓”间切换统计：

- 卡牌抓取率：某张牌在奖励中被选择的次数 ÷ 该牌在奖励中出现的次数。
- 谋司马懿击杀率：击败谋司马懿的跑局 ÷ 实际进入谋司马懿战斗的跑局。
- 谋司马懿被击杀率：死于谋司马懿的跑局 ÷ 实际进入谋司马懿战斗的跑局。
- 平均跑局时长和中位跑局时长。
- 总体胜率、平均到达楼层、匿名安装数、会话数、异常/会话比例。
- 每日跑局趋势、游戏版本分布、角色分布和常见死亡遭遇。

角色筛选作用于跑局、胜率、时长、卡牌抓取、死亡遭遇及谋司马懿战果。基础会话、匿名安装数与异常事件没有对应跑局角色，继续按整个 Mod 统计，并在网页页脚明确提示。

这些指标直接来自 `run_history.completed`、`session_start` 和 `exception`。不需要更新 Mod，不会增加新的收集类别，也不会绕过玩家在游戏内的遥测授权。

### 第一次使用：创建查询密钥

这里需要的是 **PostHog 查询密钥**，不是以 `phc_` 开头、用于上传事件的 Project token。Project token 只能写入事件，不能读取报表数据。

1. 登录 [PostHog 美国区](https://us.posthog.com)。
2. 点击左下角头像或组织菜单，进入 **Settings**。
3. 找到 **Personal API keys**，创建一个新密钥，名称可以写 `武诸葛亮本地仪表盘`。
4. 如果页面允许选择权限，只授予当前项目的事件读取权限；不要授予写入、删除或组织管理权限。
5. 创建后先不要把密钥发给任何人。PostHog 通常只完整显示一次，请暂时保留该页面。
6. Project ID 是项目的数字编号，可以在 PostHog 项目设置中查看；浏览器地址中 `/project/` 后面的数字通常也是它。

### 第二步：在本机保存连接配置

打开 PowerShell：

```powershell
cd "E:\sts2mod\武诸葛亮遥测"
npm.cmd run dashboard:setup
```

按提示填写：

1. 区域地址直接回车，使用默认的 `https://us.posthog.com`。
2. 输入数字 Project ID。
3. 粘贴 Personal API key。终端只显示星号，不会回显明文。

配置只保存在：

```text
E:\sts2mod\武诸葛亮遥测\dashboard\config.local.json
```

该文件已加入 `.gitignore`。不要把它上传、截图或发送给别人；浏览器网页也不会拿到其中的密钥。

### 第三步：启动并自动打开网页

```powershell
cd "E:\sts2mod\武诸葛亮遥测"
npm.cmd run dashboard
```

成功后会自动用默认浏览器打开：

```text
http://127.0.0.1:4173
```

网页只监听本机 `127.0.0.1`，局域网和互联网不能直接访问。保持 PowerShell 窗口开启；需要关闭仪表盘时，在该窗口按 `Ctrl+C`。这不会影响游戏，也不会停止线上 Worker。

仪表盘会在打开时自动连接 PostHog。切换时间范围或点击“刷新数据”可以重新查询；相同范围在 30 秒内会使用短暂本地缓存，避免频繁请求。

### 数据完整性保护（2026-09-02）

- 统计已从旧 `/events/` 接口迁移到 PostHog Query API；不再把旧接口的 `next: null` 当作历史已经读完。
- 每次读取固定同一 UTC 时间范围，按“时间戳 + 事件 UUID”分页；同一时刻的多条事件也不会因分页遗漏。“全部数据”不带默认的 24 小时下限。
- 先查询每类事件总数，读取后再次核对总数。只有跑局、会话、异常三类均完整时才更新页面。
- 查询失败、分页异常或数量不一致时，不发布部分统计：服务本次运行中已有同范围的完整结果则保留，并明确显示旧结果时间；没有完整缓存时显示失败，不以 0 冒充结果。
- 单类超过 20,000 条时会要求缩小时间范围，不会显示截断统计。缓存仅在本次服务进程内保存，不落盘保存玩家原始历史。
- 更新仪表盘代码后需重启本地仪表盘服务，再刷新网页；不需要重新安装游戏 Mod，也不改变玩家遥测授权或上传内容。
- 回归测试：`npm.cmd run dashboard:check` 和 `npm.cmd test`，覆盖短页、同时间戳分页、132 条误读成 5 条、查询中断及完整缓存保护。

接口依据：[PostHog Query API](https://posthog.com/docs/api/queries)。

### 指标口径说明

- **平均游玩时长**当前严格指“平均跑局时长”，取结算历史中的 `run_time_seconds`。它比进程在线时长可靠，不会把菜单挂机算成游玩。
- **谋司马懿击杀/被击杀**优先使用跑局历史提供的 `killed_by_encounter`；新版历史不再提供该字段时，以“未胜利且最后访问房间是谋司马懿”识别被击杀，其余已进入该 Boss 房的跑局计为击杀。由于主动放弃不会上传跑局历史，这个口径不会把中途放弃误判为击杀。
- **卡牌出现次数**来自游戏保存的 `card_choices`。跳过卡牌奖励，以及重掷前看到但没选的牌，都会计入出现次数。
- 样本太少时比例容易剧烈波动。仪表盘默认只显示至少出现 5 次的本 Mod 卡牌，作平衡判断时建议至少累计 25 次出现。
- “匿名安装数”来自 RitsuLib 的匿名安装 ID，不是 Steam ID，也不是实名玩家数。

### 仪表盘排错

#### 提示“尚未配置仪表盘”

先运行：

```powershell
npm.cmd run dashboard:setup
```

#### 返回 401 或 403

- 确认粘贴的是 Personal API key，而不是 `phc_` Project token。
- 确认密钥有当前项目的事件读取权限。
- 确认 Project ID 属于同一个 PostHog 项目。
- 重新运行 `npm.cmd run dashboard:setup` 可以覆盖本地配置。

#### 页面有事件但指标全是 0

1. 在 PostHog Activity 中确认存在 `run_history.completed`。
2. 游戏内需要允许跑局历史遥测。
3. 至少正常结算一局；主动放弃的跑局不会上传跑局历史。
4. 将网页时间范围切换成“全部数据”，再点“刷新数据”。

#### 4173 端口被占用

临时改用其他端口：

```powershell
$env:WUZHUGELIANG_DASHBOARD_PORT=4174
npm.cmd run dashboard
```

然后访问 `http://127.0.0.1:4174`。

### 推荐继续观察的项目

- 按进阶、角色、玩家人数拆分谋司马懿胜率，避免总体数字掩盖某个群体的异常难度。
- 各卡牌的出现样本量、抓取率和版本间变化，用来识别过强、过弱或理解成本过高的牌。
- 平均到达楼层和死亡遭遇排行，用来定位难度尖峰。
- 版本更新前后的异常/会话比例，用来发现兼容性回归。
- 游戏版本与 Mod 组合分布，用来判断问题是否集中在旧版本或特定联机环境。

不建议收集玩家姓名、Steam ID、IP、聊天内容或存档原文；这些数据对平衡分析没有必要。

## 官方参考

- [Cloudflare：Wrangler 命令与本地安装](https://developers.cloudflare.com/workers/wrangler/commands/)
- [Cloudflare：Worker Secrets](https://developers.cloudflare.com/workers/configuration/secrets/)
- [Cloudflare：Wrangler 登录](https://developers.cloudflare.com/workers/wrangler/commands/general/)
- [PostHog：事件与 Activity 页面](https://posthog.com/docs/data/events)
- [PostHog API：项目事件读取接口](https://eu.posthog.com/api/schema/swagger-ui/)
