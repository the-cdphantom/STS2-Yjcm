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
/// 《一枕清梦》与“孙乾宇宙”的可选联动。龙套演员打出时，结算改为操作“灼烧”。
/// 卡面是否改成紫色灼烧、以及是否显示「※模组联动」悬停，由 <see cref="SunqianLinkHover"/> 决定。
/// </summary>
internal static class YiZhenQingMengInterop
{
    private const string EffectTextArg = "DreamFireText";
    private const string FireAttackTextKey = "YJCM_CARD_YI_ZHEN_QING_MENG.fireAttackInlineText";
    private const string BurningTextKey = "YJCM_CARD_YI_ZHEN_QING_MENG.burningInlineText";

    public static bool IsModReady => SunqianUniverseInterop.IsReady;

    public static bool Affects(Player? player) =>
        IsModReady && SunqianUniverseInterop.IsSupportingActorCharacter(player?.Character);

    public static IEnumerable<IHoverTip> EffectHoverTips(CardModel card)
    {
        _ = card;
        if (SunqianLinkHover.ShowsBurningKeyword())
        {
            if (SunqianUniverseInterop.CreateBurningHoverTip() is { } burningTip)
                yield return burningTip;
            yield break;
        }

        yield return HoverTipFactory.FromPower<HuoGongPower>();
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
        description.Add(
            EffectTextArg,
            new LocString(
                "cards",
                SunqianLinkHover.ShowsBurningKeyword() ? BurningTextKey : FireAttackTextKey)
                .GetFormattedText());
    }

    public static async Task ApplyEffect(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature applier,
        CardModel cardSource,
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

    public static PowerModel? FindEffect(Creature target, bool useBurning) =>
        useBurning
            ? target.Powers?.FirstOrDefault(SunqianUniverseInterop.IsBurningPower)
            : target.Powers?.OfType<HuoGongPower>().FirstOrDefault();
}
