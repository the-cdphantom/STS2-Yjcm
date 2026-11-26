using STS2RitsuLib;
using STS2RitsuLib.Telemetry;

namespace Yjcm;

/// <summary>
/// 一将成名遥测注册入口，覆盖武诸葛亮、威董卓与威曹操。只有玩家在 RitsuLib 授权界面明确同意后才会发送对应类别。
/// </summary>
public static class WuZhugeliangTelemetry
{
    // 只使用当前模组 ID；旧申请方数据不再兼容或读取。
    private const string TelemetryApplicantId = "Yjcm";
    // Cloudflare Worker 部署完成后填写根地址，例如 https://wuzhugeliang-telemetry.example.workers.dev。
    // 留空时不会注册遥测申请方，也不会向玩家弹出授权提示。
    private const string WorkerHost = "https://wuzhugeliang-telemetry.cdphantom-mod.workers.dev";

    private static int _registrationState;

    /// <summary>注册遥测申请方；可由正常模组入口或双版本 Loader 安全重复调用。</summary>
    public static void Register()
    {
        if (string.IsNullOrWhiteSpace(WorkerHost))
        {
            MainFile.Logger.Warn("遥测后端尚未配置，本次不注册遥测申请方");
            return;
        }

        if (Interlocked.CompareExchange(ref _registrationState, 1, 0) != 0)
            return;

        try
        {
            TelemetryRegistry.RegisterApplicant(new TelemetryApplicant
            {
                ApplicantId = TelemetryApplicantId,
                OwnerModId = MainFile.ModId,
                DisplayName = "三国杀：一将成名mod",
                Adapter = new PostHogTelemetryAdapter(
                    host: WorkerHost,
                    projectApiKey: "proxy"),
                Requests =
                [
                    TelemetryRequest.BasicUsage(
                        "记录游戏与本 Mod 版本、系统、语言和匿名编号，用于了解各版本的游玩人数与适配情况。"),
                    TelemetryRequest.ModInventory(
                        "记录当前开启的 Mod 名称与版本（不含电脑文件路径），用于排查 Mod 之间的冲突。"),
                    TelemetryRequest.Diagnostics(
                        "记录游戏报错与闪退信息（已自动隐藏个人路径），用于快速定位和修复 Bug。"),
                    TelemetryRequest.RunHistoryFiltered(
                        "记录正常结束对局的角色、卡组、遗物与通关结果（中途放弃不统计），用于调整武诸葛亮、威董卓和威曹操的平衡性。",
                        captureFilter: context => context.SourceData is RunEndedEvent { IsAbandoned: false }),
                ],
            });

            MainFile.Logger.Info($"遥测申请方已注册，发送端点: {WorkerHost}");
        }
        catch
        {
            Interlocked.Exchange(ref _registrationState, 0);
            throw;
        }
    }
}
