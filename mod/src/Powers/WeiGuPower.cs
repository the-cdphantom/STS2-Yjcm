using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

#nullable enable

namespace Yjcm;

/// <summary>
/// 维谷：使用单目标攻击牌时，可弃置一张以玩家为目标的牌，获得1颗星。
/// </summary>
[RegisterPower]
public sealed class WeiGuPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override string CustomIconPath => "res://Yjcm/Images/Powers/wei_gu.png";
    public override string CustomBigIconPath => "res://Yjcm/Images/Powers/wei_gu.png";

    private static readonly LocString SelectPrompt = new("cards", "weigu.select_prompt");

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);
        if (cardPlay.Card.Owner?.Creature != Owner) return;

        // 仅单目标攻击牌
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (cardPlay.Target == null || cardPlay.Target.IsPlayer) return;

        // 查找手牌中可弃置的玩家目标牌
        var handPile = PileTypeExtensions.GetPile(PileType.Hand, cardPlay.Card.Owner);
        var selfTargetCards = handPile?.Cards?
            .Where(c => c != cardPlay.Card && (c.TargetType == TargetType.Self || c.TargetType == TargetType.None))
            .ToList();

        if (selfTargetCards == null || selfTargetCards.Count == 0) return;

        // 让玩家选择要弃置的牌（可以跳过）
        var chosen = await CardSelectCmd.FromHandForDiscard(choiceContext, cardPlay.Card.Owner,
            new CardSelectorPrefs(SelectPrompt, 0, 1),
            c => selfTargetCards.Contains(c), this);
        var toDiscard = chosen.FirstOrDefault();
        if (toDiscard == null) return;

        await CardCmd.Discard(choiceContext, toDiscard);
        await XingPower.Gain(choiceContext, Owner, 1, Owner, null);
    }
}
