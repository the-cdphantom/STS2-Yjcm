using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;

#nullable enable

namespace Yjcm.Interop;

/// <summary>
/// 联动提示分三种，互不叠加。
/// A. 不显示任何联动信息。
/// B. 只显示「※模组联动」悬停提示，不改卡面。
/// C. 只把卡面上的火攻关键词换成紫色灼烧，不显示「※模组联动」悬停。
/// </summary>
internal static class SunqianLinkHover
{
    public const string TitleKey = "sunqian_burning_link.hoverTitle";
    public const string BodyKey = "sunqian_burning_link.libraryHover";

    /// <summary>情况 B。</summary>
    public static bool ShouldShow()
    {
        if (!SunqianUniverseInterop.IsReady)
            return false;

        // 激活孙乾宇宙，且不在游戏中。
        if (RunManager.Instance is not { IsInProgress: true })
            return true;

        RunState? run = RunManager.Instance.DebugOnlyGetState();
        if (run == null || !PartyHasSunqian(run))
            return false;

        // 在游戏中、不在战斗中，且队伍里有孙乾宇宙角色。
        if (CombatManager.Instance is not { IsInProgress: true })
            return true;

        // 在战斗中，队伍里有孙乾宇宙角色，且当前控制的不是孙乾宇宙角色。
        return !LocalPlayerIsSunqian(run);
    }

    /// <summary>情况 C。</summary>
    public static bool ShowsBurningKeyword()
    {
        if (!SunqianUniverseInterop.IsReady)
            return false;
        if (RunManager.Instance is not { IsInProgress: true })
            return false;
        if (CombatManager.Instance is not { IsInProgress: true })
            return false;

        RunState? run = RunManager.Instance.DebugOnlyGetState();
        return run != null && LocalPlayerIsSunqian(run);
    }

    private static bool PartyHasSunqian(RunState run) =>
        run.Players.Any(player =>
            SunqianUniverseInterop.IsSupportingActorCharacter(player.Character));

    private static bool LocalPlayerIsSunqian(RunState run) =>
        run.Players.Any(player =>
            LocalContext.IsMe(player) &&
            SunqianUniverseInterop.IsSupportingActorCharacter(player.Character));
}
