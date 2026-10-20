using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

#nullable enable

namespace Yjcm.Interop;

/// <summary>
/// 《朱雀羽扇》与“孙乾宇宙”的可选联动。龙套演员攻击时，结算改为给予“灼烧”。
/// 卡面和武器说明是否改成紫色灼烧、以及是否显示「※模组联动」悬停，由 <see cref="SunqianLinkHover"/> 决定。
/// 武诸葛与威董卓共用此入口。
/// </summary>
internal static class ZhuQueYuShanInterop
{
    private const string EffectTextArg = "FanFireText";
    private const string FireAttackTextKey = "YJCM_CARD_ZHU_QUE_YU_SHAN.fireAttackInlineText";
    private const string BurningTextKey = "YJCM_CARD_ZHU_QUE_YU_SHAN.burningInlineText";

    public static bool IsModReady => SunqianUniverseInterop.IsReady;

    public static bool Affects(Player? player) =>
        IsModReady && SunqianUniverseInterop.IsSupportingActorCharacter(player?.Character);

    public static IEnumerable<IHoverTip> EffectHoverTips(CardModel card)
    {
        _ = card;
        return KeywordHoverTips();
    }

    public static IEnumerable<IHoverTip> EffectHoverTips(OrbModel orb)
    {
        _ = orb;
        return KeywordHoverTips();
    }

    public static IEnumerable<IHoverTip> LibraryHoverTips()
    {
        if (!SunqianLinkHover.ShouldShow())
            yield break;

        yield return new HoverTip(
            new LocString("cards", SunqianLinkHover.TitleKey),
            new LocString("cards", SunqianLinkHover.BodyKey));
    }

    public static void AddEffectText(LocString description, CardModel card)
    {
        _ = card;
        AddKeywordText(description);
    }

    public static void AddEffectText(LocString description, OrbModel orb)
    {
        _ = orb;
        AddKeywordText(description);
    }

    public static async Task ApplyEffect(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature applier,
        CardModel? cardSource,
        bool useBurning)
    {
        if (useBurning)
        {
            await SunqianUniverseInterop.ApplyBurning(
                choiceContext, target, amount, applier, cardSource, false);
            return;
        }

        await PowerCmd.Apply<HuoGongPower>(
            choiceContext, target, amount, applier, cardSource, false);
    }

    private static IEnumerable<IHoverTip> KeywordHoverTips()
    {
        if (SunqianLinkHover.ShowsBurningKeyword())
        {
            if (SunqianUniverseInterop.CreateBurningHoverTip() is { } burningTip)
                yield return burningTip;
            yield break;
        }

        yield return HoverTipFactory.FromPower<HuoGongPower>();
    }

    private static void AddKeywordText(LocString description) =>
        description.Add(
            EffectTextArg,
            new LocString(
                "cards",
                SunqianLinkHover.ShowsBurningKeyword() ? BurningTextKey : FireAttackTextKey)
                .GetFormattedText());

}
