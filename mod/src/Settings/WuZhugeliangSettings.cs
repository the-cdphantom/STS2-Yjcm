using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace Yjcm;

/// <summary>一将成名模组的持久化设置数据。</summary>
public sealed class WuZhugeliangSettingsData
{
    public bool EnableEffects { get; set; } = true;
    /// <summary>旧版开关，保留用于把已有设置自动迁移到四档范围。</summary>
    public bool EnableWuZhangFeiAncient { get; set; } = true;

    /// <summary>武张飞可遇见的角色范围；空字符串表示尚未从旧版开关迁移。</summary>
    public string WuZhangFeiEncounterScope { get; set; } = "";

    /// <summary>谋司马懿可遇见的角色范围。</summary>
    public string MouSimaYiEncounterScope { get; set; } = "all_characters";

    /// <summary>共享遗物进入新奖励和商店候选的角色范围。</summary>
    public string SharedRelicScope { get; set; } = "all_characters";

    /// <summary>Mod 专属随机事件进入事件候选池的角色范围。</summary>
    public string ModEventScope { get; set; } = "all_characters";

    /// <summary>谋司马懿战中同时存在尽瘁形态时优先播放哪一首曲目。</summary>
    public string MouSimaYiBgmPriority { get; set; } = "mou_sima_yi";

    /// <summary>谋司马懿战斗曲相对游戏音乐音量的百分比。</summary>
    public int MouSimaYiBgmVolume { get; set; } = 100;

    /// <summary>尽瘁形态战斗曲相对游戏音乐音量的百分比。</summary>
    public int JinCuiBgmVolume { get; set; } = 80;

    /// <summary>是否播放谋司马懿位于屏幕中央的攻击/技能特效。</summary>
    public bool EnableMouSimaYiEffects { get; set; } = true;

    /// <summary>武诸葛亮当前皮肤：classic 或 mou_zhugeliang。</summary>
    public string WuZhugeliangSkin { get; set; } = "classic";
    public string WeiDongZhuoSkin { get; set; } = "classic";

    /// <summary>勾选“以后不再弹出”时对应的 Mod 版本；版本改变会自然失效。</summary>
    public string AntiPiracyNoticeDismissedVersion { get; set; } = "";

    /// <summary>已经展示过公告入口的 Mod 版本；版本改变后需重新提示。</summary>
    public string AntiPiracyNoticeSeenVersion { get; set; } = "";

    /// <summary>上一次主菜单展示反盗版公告或提示的 UTC 日期（yyyy-MM-dd）。</summary>
    public string AntiPiracyNoticeLastPromptedUtcDate { get; set; } = "";

    /// <summary>扫荡模式：custom、random_battle 或 one_click。</summary>
    public string SweepMode { get; set; } = "custom";

    /// <summary>普通战斗中，每名初始敌人的逃跑概率。</summary>
    public int SweepNormalChance { get; set; }

    /// <summary>精英战斗中，每名初始敌人的逃跑概率。</summary>
    public int SweepEliteChance { get; set; }

    /// <summary>首领战斗中，每名初始敌人的逃跑概率。</summary>
    public int SweepBossChance { get; set; }
#if WEI_DONG_ZHUO
    /// <summary>犷勇语音：every_trigger 为每张触发牌一次；once_per_turn 为每回合一次。</summary>
    public string GuangYongVoiceFrequency { get; set; } = "every_trigger";
#endif
}

/// <summary>注册模组设置页并提供安全的运行时读取入口。</summary>
public static class WuZhugeliangSettings
{
    internal const string AuthorHomepageUrl = "https://space.bilibili.com/323588945?spm_id_from=333.1007.0.0";
    internal const string AuthorSponsorUrl = "https://ifdian.net/a/CDphantom";
    public const string AllCharacters = "all_characters";
    public const string OwnModCharacters = "own_mod_characters";
    public const string ModCharacters = "mod_characters";
    public const string NoCharacters = "no_characters";
    public const string PreferMouSimaYiBgm = "mou_sima_yi";
    public const string PreferJinCuiBgm = "jin_cui";
    public const string ClassicWuZhugeliangSkin = "classic";
    public const string MouZhugeliangSkin = "mou_zhugeliang";
    public const string ClassicWeiDongZhuoSkin = "classic";
    public const string TaiShanBengShiSkin = "tai_shan_beng_shi";
    public const string SweepModeCustom = "custom";
    public const string SweepModeRandomBattle = "random_battle";
    public const string SweepModeOneClick = "one_click";
#if WEI_DONG_ZHUO
    public const string GuangYongVoiceEveryTrigger = "every_trigger";
    public const string GuangYongVoiceOncePerTurnValue = "once_per_turn";
#endif

    private const string DataKey = "settings";
    private const string FileName = "settings.json";

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> WuZhangFeiBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => NormalizeScope(settings.WuZhangFeiEncounterScope, settings.EnableWuZhangFeiAncient),
        static (settings, value) =>
        {
            string scope = NormalizeScope(value, settings.EnableWuZhangFeiAncient);
            settings.WuZhangFeiEncounterScope = scope;
            settings.EnableWuZhangFeiAncient = scope != NoCharacters;
        });

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> MouSimaYiBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => NormalizeScope(settings.MouSimaYiEncounterScope, true),
        static (settings, value) => settings.MouSimaYiEncounterScope = NormalizeScope(value, true));

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> SharedRelicBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => NormalizeScope(settings.SharedRelicScope, true),
        static (settings, value) => settings.SharedRelicScope = NormalizeScope(value, true));

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> ModEventBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => NormalizeScope(settings.ModEventScope, true),
        static (settings, value) => settings.ModEventScope = NormalizeScope(value, true));

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> BgmPriorityBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => NormalizeBgmPriority(settings.MouSimaYiBgmPriority),
        static (settings, value) =>
        {
            settings.MouSimaYiBgmPriority = NormalizeBgmPriority(value);
            MouSimaYiBgmPlayer.ReconcileMusicPriority();
        });

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, int> MouSimaYiVolumeBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => Math.Clamp(settings.MouSimaYiBgmVolume, 0, 100),
        static (settings, value) =>
        {
            settings.MouSimaYiBgmVolume = Math.Clamp(value, 0, 100);
            MouSimaYiBgmPlayer.ApplyVolumeSetting();
        });

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, int> JinCuiVolumeBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => Math.Clamp(settings.JinCuiBgmVolume, 0, 100),
        static (settings, value) =>
        {
            settings.JinCuiBgmVolume = Math.Clamp(value, 0, 100);
            JinCuiPower.ApplyBgmVolumeSetting();
        });

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, bool> EffectsBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.EnableEffects,
        static (settings, value) =>
        {
            settings.EnableEffects = value;
            if (!value)
            {
                CardEffectVfx.HideActive();
                ShiWeiYanVfx.HideActive();
                MouSimaYiSpineVisuals.HideActionOverlay();
#if WEI_DONG_ZHUO
                WeiDongZhuoComboVfx.HideActive();
#endif
            }
        });

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> WuZhugeliangSkinBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => NormalizeWuZhugeliangSkin(settings.WuZhugeliangSkin),
        static (settings, value) => settings.WuZhugeliangSkin = NormalizeWuZhugeliangSkin(value));

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> WeiDongZhuoSkinBinding = new(
        MainFile.ModId, DataKey, SaveScope.Global,
        static settings => NormalizeWeiDongZhuoSkin(settings.WeiDongZhuoSkin),
        static (settings, value) => settings.WeiDongZhuoSkin = NormalizeWeiDongZhuoSkin(value));

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> AntiPiracyNoticeDismissedVersionBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.AntiPiracyNoticeDismissedVersion ?? "",
        static (settings, value) => settings.AntiPiracyNoticeDismissedVersion = value ?? "");

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> AntiPiracyNoticeSeenVersionBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.AntiPiracyNoticeSeenVersion ?? "",
        static (settings, value) => settings.AntiPiracyNoticeSeenVersion = value ?? "");

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> AntiPiracyNoticeLastPromptedDateBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.AntiPiracyNoticeLastPromptedUtcDate ?? "",
        static (settings, value) => settings.AntiPiracyNoticeLastPromptedUtcDate = value ?? "");

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> SweepModeBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => NormalizeSweepMode(settings.SweepMode),
        static (settings, value) => settings.SweepMode = NormalizeSweepMode(value));

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, int> SweepNormalChanceBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => Math.Clamp(settings.SweepNormalChance, 0, 100),
        static (settings, value) => settings.SweepNormalChance = Math.Clamp(value, 0, 100));

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, int> SweepEliteChanceBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => Math.Clamp(settings.SweepEliteChance, 0, 100),
        static (settings, value) => settings.SweepEliteChance = Math.Clamp(value, 0, 100));

    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, int> SweepBossChanceBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => Math.Clamp(settings.SweepBossChance, 0, 100),
        static (settings, value) => settings.SweepBossChance = Math.Clamp(value, 0, 100));
#if WEI_DONG_ZHUO
    private static readonly ModSettingsValueBinding<WuZhugeliangSettingsData, string> GuangYongVoiceBinding = new(
        MainFile.ModId,
        DataKey,
        SaveScope.Global,
        static settings => NormalizeGuangYongVoiceFrequency(settings.GuangYongVoiceFrequency),
        static (settings, value) => settings.GuangYongVoiceFrequency = NormalizeGuangYongVoiceFrequency(value));
#endif

    // 0=未注册，1=注册中，2=注册完成。
    private static int _registrationState;
    private static int _readFailureLogged;
    private static int _mouSimaYiReadFailureLogged;
    private static int _sharedRelicReadFailureLogged;
    private static int _modEventReadFailureLogged;
    private static int _mediaReadFailureLogged;
    private static string? _pendingWuZhugeliangSkin;
    private static string? _pendingWeiDongZhuoSkin;

    /// <summary>
    /// 获取武张飞可遇见的角色范围。设置系统尚未就绪或读取失败时保持默认全角色可遇见。
    /// </summary>
    public static string WuZhangFeiEncounterScope
    {
        get
        {
            if (Volatile.Read(ref _registrationState) != 2)
                return AllCharacters;

            try
            {
                return WuZhangFeiBinding.Read();
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref _readFailureLogged, 1) == 0)
                    MainFile.Logger.Warn($"读取武张飞先古设置失败，按默认全角色可遇见处理: {ex.Message}");
                return AllCharacters;
            }
        }
    }

    /// <summary>
    /// 获取谋司马懿可遇见的角色范围。设置系统尚未就绪或读取失败时保持默认全角色可遇见。
    /// </summary>
    public static string MouSimaYiEncounterScope
    {
        get
        {
            if (Volatile.Read(ref _registrationState) != 2)
                return AllCharacters;

            try
            {
                return MouSimaYiBinding.Read();
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref _mouSimaYiReadFailureLogged, 1) == 0)
                    MainFile.Logger.Warn($"读取谋司马懿首领设置失败，按默认全角色可遇见处理: {ex.Message}");
                return AllCharacters;
            }
        }
    }

    /// <summary>未完成注册或读取失败时，维持默认全角色可获得共享遗物。</summary>
    public static string SharedRelicScope
    {
        get
        {
            if (Volatile.Read(ref _registrationState) != 2)
                return AllCharacters;

            try
            {
                return SharedRelicBinding.Read();
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref _sharedRelicReadFailureLogged, 1) == 0)
                    MainFile.Logger.Warn($"读取共享遗物设置失败，按默认全角色处理: {ex.Message}");
                return AllCharacters;
            }
        }
    }

    /// <summary>未完成注册或读取失败时，维持默认全角色可遇到 Mod 专属随机事件。</summary>
    public static string ModEventScope
    {
        get
        {
            if (Volatile.Read(ref _registrationState) != 2)
                return AllCharacters;

            try
            {
                return ModEventBinding.Read();
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref _modEventReadFailureLogged, 1) == 0)
                    MainFile.Logger.Warn($"读取 Mod 专属事件设置失败，按默认全角色处理: {ex.Message}");
                return AllCharacters;
            }
        }
    }

    /// <summary>按先古和首领使用的程序集归属规则判断指定角色，供扫荡和共享遗物使用。</summary>
    internal static bool MatchesCharacterScope(string scope, Type characterType) =>
        NormalizeScope(scope, true) switch
        {
            OwnModCharacters => characterType.Assembly == typeof(WuZhugeliang).Assembly,
            ModCharacters => characterType.Assembly != typeof(CharacterModel).Assembly,
            NoCharacters => false,
            _ => true
        };

    /// <summary>多人队伍中任一角色符合范围即可；用于 Mod 专属随机事件的准入判断。</summary>
    internal static bool MatchesAnyPlayerScope(IRunState runState, string scope) =>
        runState.Players.Any(player =>
            MatchesCharacterScope(scope, player.Character.GetType()));

    public static string MouSimaYiBgmPriority =>
        ReadMediaSetting(BgmPriorityBinding, PreferMouSimaYiBgm);

    public static int MouSimaYiBgmVolume =>
        Math.Clamp(ReadMediaSetting(MouSimaYiVolumeBinding, 100), 0, 100);

    public static int JinCuiBgmVolume =>
        Math.Clamp(ReadMediaSetting(JinCuiVolumeBinding, 80), 0, 100);

    public static bool EnableEffects => ReadMediaSetting(EffectsBinding, true);
    public static bool EnableMouSimaYiEffects => EnableEffects;

    public static string CurrentWuZhugeliangSkin
    {
        get
        {
            string? pending = Volatile.Read(ref _pendingWuZhugeliangSkin);
            return pending ?? ReadMediaSetting(WuZhugeliangSkinBinding, ClassicWuZhugeliangSkin);
        }
    }

    public static bool UseMouZhugeliangSkin =>
        CurrentWuZhugeliangSkin == MouZhugeliangSkin;

    public static string CurrentWeiDongZhuoSkin => Volatile.Read(ref _pendingWeiDongZhuoSkin)
        ?? ReadMediaSetting(WeiDongZhuoSkinBinding, ClassicWeiDongZhuoSkin);

    public static bool UseTaiShanBengShiSkin => CurrentWeiDongZhuoSkin == TaiShanBengShiSkin;

    internal static void SetWeiDongZhuoSkin(bool useTaiShanSkin)
    {
        string value = useTaiShanSkin ? TaiShanBengShiSkin : ClassicWeiDongZhuoSkin;
        Volatile.Write(ref _pendingWeiDongZhuoSkin, value);
        if (Volatile.Read(ref _registrationState) != 2) return;
        try
        {
            WeiDongZhuoSkinBinding.Write(value);
            WeiDongZhuoSkinBinding.Save();
            Volatile.Write(ref _pendingWeiDongZhuoSkin, null);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"保存威董卓皮肤设置失败，本次会话仍使用所选皮肤：{ex.Message}");
        }
    }

    public static string CurrentSweepMode =>
        NormalizeSweepMode(ReadMediaSetting(SweepModeBinding, SweepModeCustom));

    public static int SweepNormalChance =>
        Math.Clamp(ReadMediaSetting(SweepNormalChanceBinding, 0), 0, 100);

    public static int SweepEliteChance =>
        Math.Clamp(ReadMediaSetting(SweepEliteChanceBinding, 0), 0, 100);

    public static int SweepBossChance =>
        Math.Clamp(ReadMediaSetting(SweepBossChanceBinding, 0), 0, 100);

    internal static void SetWuZhugeliangSkin(bool useMouSkin)
    {
        string value = useMouSkin ? MouZhugeliangSkin : ClassicWuZhugeliangSkin;
        Volatile.Write(ref _pendingWuZhugeliangSkin, value);
        if (Volatile.Read(ref _registrationState) != 2)
            return;

        try
        {
            WuZhugeliangSkinBinding.Write(value);
            WuZhugeliangSkinBinding.Save();
            Volatile.Write(ref _pendingWuZhugeliangSkin, null);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"保存武诸葛亮皮肤设置失败，本次会话仍使用所选皮肤：{ex.Message}");
        }
    }

    internal static bool ShouldShowAntiPiracyNotice(string installedVersion) =>
        !string.Equals(ReadAntiPiracyNoticeSetting(AntiPiracyNoticeSeenVersionBinding), installedVersion,
            StringComparison.Ordinal);

    internal static bool HasSeenAnyAntiPiracyNotice() =>
        !string.IsNullOrWhiteSpace(ReadAntiPiracyNoticeSetting(AntiPiracyNoticeSeenVersionBinding));

    internal static bool ShouldShowAntiPiracyNoticeNudge() =>
        !IsAntiPiracyNoticeSuppressedForCurrentVersion() &&
        !string.Equals(ReadAntiPiracyNoticeSetting(AntiPiracyNoticeLastPromptedDateBinding),
            DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    internal static void MarkAntiPiracyNoticeSeen(string installedVersion) =>
        WriteAntiPiracyNoticeSetting(AntiPiracyNoticeSeenVersionBinding, installedVersion);

    internal static void MarkAntiPiracyNoticePromptedToday() =>
        WriteAntiPiracyNoticeSetting(AntiPiracyNoticeLastPromptedDateBinding,
            DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    internal static void SetAntiPiracyNoticeSuppressedForVersion(string installedVersion, bool suppressed) =>
        WriteAntiPiracyNoticeSetting(AntiPiracyNoticeDismissedVersionBinding, suppressed ? installedVersion : "");

    private static bool IsAntiPiracyNoticeSuppressedForCurrentVersion()
    {
        string seenVersion = ReadAntiPiracyNoticeSetting(AntiPiracyNoticeSeenVersionBinding);
        return !string.IsNullOrWhiteSpace(seenVersion) &&
               string.Equals(ReadAntiPiracyNoticeSetting(AntiPiracyNoticeDismissedVersionBinding), seenVersion,
                   StringComparison.Ordinal);
    }
#if WEI_DONG_ZHUO
    public static bool GuangYongVoiceOncePerTurn =>
        ReadMediaSetting(GuangYongVoiceBinding, GuangYongVoiceEveryTrigger) == GuangYongVoiceOncePerTurnValue;
#endif

    private static T ReadMediaSetting<T>(IModSettingsValueBinding<T> binding, T fallback)
    {
        if (Volatile.Read(ref _registrationState) != 2)
            return fallback;

        try
        {
            return binding.Read();
        }
        catch (Exception ex)
        {
            if (Interlocked.Exchange(ref _mediaReadFailureLogged, 1) == 0)
                MainFile.Logger.Warn($"读取谋司马懿音画设置失败，使用默认值: {ex.Message}");
            return fallback;
        }
    }

    private static string ReadAntiPiracyNoticeSetting(IModSettingsValueBinding<string> binding)
    {
        if (Volatile.Read(ref _registrationState) != 2)
            return "";

        try
        {
            return binding.Read() ?? "";
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"读取反盗版公告设置失败，按未抑制处理：{ex.Message}");
            return "";
        }
    }

    private static void WriteAntiPiracyNoticeSetting(IModSettingsValueBinding<string> binding, string value)
    {
        if (Volatile.Read(ref _registrationState) != 2)
            return;

        try
        {
            binding.Write(value);
            binding.Save();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"保存反盗版公告设置失败：{ex.Message}");
        }
    }

    /// <summary>校验新设置，并把旧版布尔开关映射到对应范围。</summary>
    private static string NormalizeScope(string scope, bool legacyEnabled)
    {
        return scope switch
        {
            AllCharacters => AllCharacters,
            OwnModCharacters => OwnModCharacters,
            ModCharacters => ModCharacters,
            NoCharacters => NoCharacters,
            _ => legacyEnabled ? AllCharacters : NoCharacters
        };
    }

    private static string NormalizeBgmPriority(string priority) => priority switch
    {
        PreferJinCuiBgm => PreferJinCuiBgm,
        _ => PreferMouSimaYiBgm
    };

    private static string NormalizeWuZhugeliangSkin(string skin) => skin switch
    {
        MouZhugeliangSkin => MouZhugeliangSkin,
        _ => ClassicWuZhugeliangSkin
    };

    internal static string NormalizeWeiDongZhuoSkin(string skin) =>
        skin == TaiShanBengShiSkin ? TaiShanBengShiSkin : ClassicWeiDongZhuoSkin;

    private static string NormalizeSweepMode(string mode) => mode switch
    {
        SweepModeRandomBattle => SweepModeRandomBattle,
        SweepModeOneClick => SweepModeOneClick,
        _ => SweepModeCustom
    };
#if WEI_DONG_ZHUO
    private static string NormalizeGuangYongVoiceFrequency(string frequency) => frequency switch
    {
        GuangYongVoiceOncePerTurnValue => GuangYongVoiceOncePerTurnValue,
        _ => GuangYongVoiceEveryTrigger
    };
#endif

    /// <summary>注册全局设置数据与设置页；正常入口和双版本 Loader 可安全重复调用。</summary>
    public static void Register()
    {
        if (Interlocked.CompareExchange(ref _registrationState, 1, 0) != 0)
            return;

        try
        {
            ModDataStore.For(MainFile.ModId).Register<WuZhugeliangSettingsData>(
                key: DataKey,
                fileName: FileName,
                scope: SaveScope.Global,
                defaultFactory: static () => new WuZhugeliangSettingsData(),
                autoCreateIfMissing: true);

            string? pendingSkin = Volatile.Read(ref _pendingWuZhugeliangSkin);
            if (pendingSkin != null)
            {
                WuZhugeliangSkinBinding.Write(pendingSkin);
                WuZhugeliangSkinBinding.Save();
                Volatile.Write(ref _pendingWuZhugeliangSkin, null);
            }

            string? pendingDongZhuoSkin = Volatile.Read(ref _pendingWeiDongZhuoSkin);
            if (pendingDongZhuoSkin != null)
            {
                WeiDongZhuoSkinBinding.Write(pendingDongZhuoSkin);
                WeiDongZhuoSkinBinding.Save();
                Volatile.Write(ref _pendingWeiDongZhuoSkin, null);
            }

            RitsuLibFramework.RegisterModSettings(MainFile.ModId, page =>
            {
                var builder = page
                .WithTitle(ModSettingsText.Literal("一将成名设置"))
                .WithModDisplayName(ModSettingsText.Literal("三国杀：一将成名mod"))
                .WithVisibleOnHostSurfaces(
                    ModSettingsHostSurface.MainMenu | ModSettingsHostSurface.RunPause)
                .AddSection("author", section => section
                    .WithTitle(ModSettingsText.Literal("作者"))
                    .AddButton("author_homepage", ModSettingsText.Literal("作者主页"),
                        ModSettingsText.Literal("打开作者主页"), () => { Godot.OS.ShellOpen(AuthorHomepageUrl); })
                    .AddButton("author_sponsor", ModSettingsText.Literal("赞助作者"),
                        ModSettingsText.Literal("前往赞助"), () => { Godot.OS.ShellOpen(AuthorSponsorUrl); }))
                .AddSection("visual_effects", section => section
                    .WithTitle(ModSettingsText.Literal("特效"))
                    .AddToggle("enable_effects", ModSettingsText.Literal("特效总开关"), EffectsBinding,
                        ModSettingsText.Literal("统一控制本 Mod 的卡牌与连招特效（含威曹操的卡牌和窥判特效），以及魏延、司马懿的屏幕特效。立绘、选牌界面和音效独立保留。")))
                .AddSection("anti_piracy_notice", section => section
                    .WithTitle(ModSettingsText.Literal("反盗版公告"))
                    .AddButton(
                        "view_anti_piracy_notice",
                        ModSettingsText.Literal("查看反盗版公告"),
                        ModSettingsText.Literal("打开公告"),
                        AntiPiracyNoticeController.ShowFromSettings,
                        ModSettingsButtonTone.Accent,
                        ModSettingsText.Literal("可随时查看公告。勾选“以后不再弹出”只对当前 Mod 版本生效，更新版本后会重新显示。")))
                .AddSection("sweep_mode", section => section
                    .WithTitle(ModSettingsText.Literal("一键扫荡"))
                    .AddChoice(
                        "sweep_mode",
                        ModSettingsText.Literal("扫荡模式"),
                        SweepModeBinding,
                        [
                            new ModSettingsChoiceOption<string>(SweepModeCustom, ModSettingsText.Literal("自定义概率")),
                            new ModSettingsChoiceOption<string>(SweepModeRandomBattle, ModSettingsText.Literal("每场随机概率")),
                            new ModSettingsChoiceOption<string>(SweepModeOneClick, ModSettingsText.Literal("一键扫荡"))
                        ],
                        ModSettingsText.Literal("队伍中有武诸葛亮、威董卓或威曹操时生效。自定义模式按战斗类型读取下方概率。每场随机模式为该场生成 0%～100% 的逃跑概率。一键扫荡让包括首领在内的所有初始敌人必定逃跑。联机以队伍中编号最小的本 Mod 角色玩家的设置为准。"),
                        ModSettingsChoicePresentation.Dropdown)
                    .AddIntSlider(
                        "sweep_normal_chance",
                        ModSettingsText.Literal("普通敌人逃跑概率"),
                        SweepNormalChanceBinding,
                        0,
                        100,
                        1,
                        static value => $"{value}%",
                        ModSettingsText.Literal("自定义概率模式下，普通战斗中每名初始敌人分别判定。"))
                    .AddIntSlider(
                        "sweep_elite_chance",
                        ModSettingsText.Literal("精英敌人逃跑概率"),
                        SweepEliteChanceBinding,
                        0,
                        100,
                        1,
                        static value => $"{value}%",
                        ModSettingsText.Literal("自定义概率模式下，精英战斗中每名初始敌人分别判定。"))
                    .AddIntSlider(
                        "sweep_boss_chance",
                        ModSettingsText.Literal("首领逃跑概率"),
                        SweepBossChanceBinding,
                        0,
                        100,
                        1,
                        static value => $"{value}%",
                        ModSettingsText.Literal("自定义概率模式下，首领战斗中每名初始敌人分别判定。")))
                .AddSection("shared_relics", section => section
                    .WithTitle(ModSettingsText.Literal("共享遗物"))
                    .AddChoice(
                        "shared_relic_scope",
                        ModSettingsText.Literal("共享遗物可获得范围"),
                        SharedRelicBinding,
                        [
                            new ModSettingsChoiceOption<string>(AllCharacters, ModSettingsText.Literal("全角色")),
                            new ModSettingsChoiceOption<string>(OwnModCharacters, ModSettingsText.Literal("本 Mod 角色")),
                            new ModSettingsChoiceOption<string>(ModCharacters, ModSettingsText.Literal("所有 Mod 角色")),
                            new ModSettingsChoiceOption<string>(NoCharacters, ModSettingsText.Literal("关闭"))
                        ],
                        ModSettingsText.Literal("控制之后生成的随机奖励、商店和宝箱。个人奖励按领取者判断，联机宝箱须全队符合范围。请在联机前统一设置。已有遗物、初始遗物及指定奖励沿用各自规则。"),
                        ModSettingsChoicePresentation.Dropdown))
                .AddSection("events", section => section
                    .WithTitle(ModSettingsText.Literal("随机事件"))
                    .AddChoice(
                        "mod_event_scope",
                        ModSettingsText.Literal("Mod 专属事件可遇见范围"),
                        ModEventBinding,
                        [
                            new ModSettingsChoiceOption<string>(AllCharacters, ModSettingsText.Literal("全角色")),
                            new ModSettingsChoiceOption<string>(OwnModCharacters, ModSettingsText.Literal("仅本 Mod 角色")),
                            new ModSettingsChoiceOption<string>(ModCharacters, ModSettingsText.Literal("所有 Mod 角色")),
                            new ModSettingsChoiceOption<string>(NoCharacters, ModSettingsText.Literal("关闭"))
                        ],
                        ModSettingsText.Literal("控制屈原投江、我要验牌、上方谷之雨、子午谷奇谋四个 Mod 专属随机事件是否进入候选池。多人游戏中队伍里至少一人符合即可出现；各端本地抽取，请在开局前统一档位。子午谷奇谋仍要求队伍中有武诸葛亮，上方谷之雨仍要求至少一张联动奖励牌。"),
                        ModSettingsChoicePresentation.Dropdown))
                .AddSection("ancients", section => section
                    .WithTitle(ModSettingsText.Literal("先古之民"))
                    .AddChoice(
                        "wu_zhang_fei_encounter_scope",
                        ModSettingsText.Literal("武张飞可遇见范围"),
                        WuZhangFeiBinding,
                        [
                            new ModSettingsChoiceOption<string>(AllCharacters, ModSettingsText.Literal("所有角色可遇到")),
                            new ModSettingsChoiceOption<string>(OwnModCharacters, ModSettingsText.Literal("仅本 Mod 角色可遇到")),
                            new ModSettingsChoiceOption<string>(ModCharacters, ModSettingsText.Literal("仅 Mod 角色可遇到")),
                            new ModSettingsChoiceOption<string>(NoCharacters, ModSettingsText.Literal("所有角色都无法遇到"))
                        ],
                        ModSettingsText.Literal("控制武张飞是否进入所有第三幕地图的先古候选池。多人游戏中，队伍里至少一名角色符合即可遇到；修改只影响之后生成的地图。"),
                        ModSettingsChoicePresentation.Dropdown))
                .AddSection("bosses", section => section
                    .WithTitle(ModSettingsText.Literal("首领"))
                    .AddChoice(
                        "mou_sima_yi_encounter_scope",
                        ModSettingsText.Literal("谋司马懿可遇见范围"),
                        MouSimaYiBinding,
                        [
                            new ModSettingsChoiceOption<string>(AllCharacters, ModSettingsText.Literal("所有角色可遇到")),
                            new ModSettingsChoiceOption<string>(OwnModCharacters, ModSettingsText.Literal("仅本 Mod 角色可遇到")),
                            new ModSettingsChoiceOption<string>(ModCharacters, ModSettingsText.Literal("仅 Mod 角色可遇到")),
                            new ModSettingsChoiceOption<string>(NoCharacters, ModSettingsText.Literal("所有角色都无法遇到"))
                        ],
                        ModSettingsText.Literal("控制谋司马懿是否进入所有第三幕地图的首领候选池。多人游戏中，队伍里至少一名角色符合即可遇到；修改只影响之后生成的地图。"),
                        ModSettingsChoicePresentation.Dropdown)
                    .AddChoice(
                        "mou_sima_yi_bgm_priority",
                        ModSettingsText.Literal("司马懿战斗音乐优先级"),
                        BgmPriorityBinding,
                        [
                            new ModSettingsChoiceOption<string>(PreferMouSimaYiBgm, ModSettingsText.Literal("优先播放司马懿战斗曲")),
                            new ModSettingsChoiceOption<string>(PreferJinCuiBgm, ModSettingsText.Literal("开启尽瘁后播放尽瘁曲"))
                        ],
                        ModSettingsText.Literal("只影响谋司马懿战斗中已经开启尽瘁形态的情况，两首曲目不会同时播放。"),
                        ModSettingsChoicePresentation.Dropdown)
                    .AddIntSlider(
                        "mou_sima_yi_bgm_volume",
                        ModSettingsText.Literal("司马懿战斗曲音量"),
                        MouSimaYiVolumeBinding,
                        0,
                        100,
                        5,
                        static value => $"{value}%",
                        ModSettingsText.Literal("相对于游戏主音量和音乐音量进行调整。"))
                    .AddIntSlider(
                        "jin_cui_bgm_volume",
                        ModSettingsText.Literal("尽瘁战斗曲音量"),
                        JinCuiVolumeBinding,
                        0,
                        100,
                        5,
                        static value => $"{value}%",
                        ModSettingsText.Literal("相对于游戏主音量和音乐音量进行调整。")));
#if WEI_DONG_ZHUO
                builder.AddSection("wei_dong_zhuo", section => section
                    .WithTitle(ModSettingsText.Literal("威董卓"))
                    .AddChoice(
                        "guang_yong_voice_frequency",
                        ModSettingsText.Literal("犷勇音效播放频率"),
                        GuangYongVoiceBinding,
                        [
                            new ModSettingsChoiceOption<string>(GuangYongVoiceEveryTrigger, ModSettingsText.Literal("每次触发遗物都播放音效")),
                            new ModSettingsChoiceOption<string>(GuangYongVoiceOncePerTurnValue, ModSettingsText.Literal("每回合只播放一次"))
                        ],
                        ModSettingsText.Literal("群攻仍按一张牌的一次遗物触发计算；默认每次触发都播放。"),
                        ModSettingsChoicePresentation.Dropdown));
#endif
            });

            Volatile.Write(ref _registrationState, 2);
            MainFile.Logger.Info("模组设置页注册完成");
        }
        catch
        {
            Volatile.Write(ref _registrationState, 0);
            throw;
        }
    }
}
