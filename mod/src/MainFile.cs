using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
#if GAME_BETA
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
#else
#endif
using Godot.Bridge;
using STS2RitsuLib;
using STS2RitsuLib.Audio;
using STS2RitsuLib.Interop;

namespace Yjcm;

[ModInitializer(nameof(Initialize))]
public class MainFile
{
    public const string ModId = "Yjcm";
    private static readonly object InitializationGate = new();
    private static bool _initializing;
    private static bool _initialized;

    public static Logger Logger { get; } =
        new(ModId, LogType.Generic);

    public static void Initialize()
    {
        lock (InitializationGate)
        {
            if (_initialized || _initializing) return;
            _initializing = true;
            try
            {
                InitializeCore();
                _initialized = true;
            }
            finally { _initializing = false; }
        }
    }

    private static void InitializeCore()
    {
        ScriptManagerBridge.LookupScriptsInAssembly(Assembly.GetExecutingAssembly());

        // RitsuLib 自动注册（教程要求的初始化步骤）
        RitsuLibFramework.EnsureGodotScriptsRegistered(Assembly.GetExecutingAssembly(), Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, Assembly.GetExecutingAssembly());
#if GAME_BETA
#else
        Logger.Info("初始化步骤1完成: 内容注册");
#endif

        // 注册玩家设置页
        try { WuZhugeliangSettings.Register(); }
        catch (Exception ex) { Logger.Warn($"设置页注册失败（继续）: {ex.Message}"); }

        PlayerSkins.Register();

        // 注册遥测申请方；后端未配置时保持完全禁用
        try { WuZhugeliangTelemetry.Register(); }
        catch (Exception ex) { Logger.Warn($"遥测注册失败（继续）: {ex.Message}"); }

        // 注册 FMOD Bank
#if GAME_BETA
#else
        try
        {
#endif
            FmodStudioDeferredBankRegistration.RegisterBank("res://Yjcm/audios/liuchuqishan.bank");
            FmodStudioDeferredBankRegistration.RegisterStudioGuidMappings("res://Yjcm/audios/GUIDs.txt");
#if GAME_BETA
#else
            Logger.Info("初始化步骤2完成: FMOD Bank");
        }
        catch (Exception ex) { Logger.Warn($"FMOD 注册失败（继续）: {ex.Message}"); }
#endif

        // 古老牙齿先古转化：激将 → 仁彀
#if GAME_BETA
        RitsuLibFramework.RegisterArchaicToothTranscendenceMapping<LiuBeiJiJiang, WeiLiuBeiRenGou>();
#else
        try { RitsuLibFramework.RegisterArchaicToothTranscendenceMapping<LiuBeiJiJiang, WeiLiuBeiRenGou>(); }
        catch (Exception ex) { Logger.Warn($"激将转化注册失败（继续）: {ex.Message}"); }
#endif
#if WEI_DONG_ZHUO
#if GAME_BETA
        RitsuLibFramework.RegisterArchaicToothTranscendenceMapping(new ModelId("CARD", "YJCM_CARD_WEI_DONG_ZHUO_JU_CHUI"), typeof(WeiDongZhuoJieJuChui), ModId);
        RitsuLibFramework.RegisterDustyTomeCard(new ModelId("CHARACTER", "YJCM_CHARACTER_WEI_DONG_ZHUO"), typeof(WeiDongZhuoDongZhuoHuoShaoLuoYang), ModId);
#else
        try { RitsuLibFramework.RegisterArchaicToothTranscendenceMapping(new ModelId("CARD", "YJCM_CARD_WEI_DONG_ZHUO_JU_CHUI"), typeof(WeiDongZhuoJieJuChui), ModId); }
        catch (Exception ex) { Logger.Warn($"据陲转化注册失败（继续）: {ex.Message}"); }
        try { RitsuLibFramework.RegisterDustyTomeCard(new ModelId("CHARACTER", "YJCM_CHARACTER_WEI_DONG_ZHUO"), typeof(WeiDongZhuoDongZhuoHuoShaoLuoYang), ModId); }
        catch (Exception ex) { Logger.Warn($"火烧洛阳魔典候选注册失败（继续）: {ex.Message}"); }
#endif
#endif
        // 欧洛巴斯之触遗物升级：情势 → 界情势
#if GAME_BETA
        RitsuLibFramework.RegisterTouchOfOrobasRefinementMapping<YjcmQingShi, YjcmJieQingShi>();
#else
        try { RitsuLibFramework.RegisterTouchOfOrobasRefinementMapping<YjcmQingShi, YjcmJieQingShi>(); }
        catch (Exception ex) { Logger.Warn($"界情势升级注册失败（继续）: {ex.Message}"); }
        Logger.Info("初始化步骤3完成: 转化/升级映射");
#endif

        // Harmony 补丁：逐个挂载，单个失败只警告不影响其他（防止某目标在 0.107.1 缺失时拖垮全部补丁）
        Harmony harmony = new(ModId);
        int ok = 0, fail = 0;
        foreach (var cls in Assembly.GetExecutingAssembly().GetTypes()
                     .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0))
        {
            try
            {
                harmony.CreateClassProcessor(cls).Patch();
                ok++;
            }
            catch (Exception ex)
            {
                fail++;
                Logger.Warn($"补丁挂载失败（已跳过）: {cls.Name}: {ex.Message}");
            }
        }
        Logger.Info($"初始化步骤4完成: Harmony 补丁 成功={ok} 失败={fail}");

        Api.PublicApi.Initialize([
            "YJCM_CHARACTER_WU_ZHUGELIANG",
#if WEI_DONG_ZHUO
            "YJCM_CHARACTER_WEI_DONG_ZHUO"
#endif
        ]);
        Logger.Info("一将成名 Mod 加载成功，双向联动 API v1 已就绪。");
    }
}
