using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable

namespace Yjcm;

/// <summary>原版白银狮子：持有者受到的全部伤害均被封顶为 1 点。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class YuanBanBaiYinShiZi : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override string PackedIconPath => "res://Yjcm/Images/Relics/yuan_ban_bai_yin_shi_zi.png";
    protected override string PackedIconOutlinePath => "res://Yjcm/Images/Relics/yuan_ban_bai_yin_shi_zi.png";
    protected override string BigIconPath => "res://Yjcm/Images/Relics/Big/yuan_ban_bai_yin_shi_zi.png";

#if GAME_BETA
    public override decimal ModifyDamageCap(
        Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner?.Creature ? 1m : decimal.MaxValue;
#else
    public override decimal ModifyDamageCap(
        Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner?.Creature ? 1m : decimal.MaxValue;
#endif
}

/// <summary>
/// 高达一号残魂：手牌始终维持四张；超过时必须由玩家选择弃置，多于时立即补摸。
/// 每次使用牌获得 6 点格挡并随机造成 6 点伤害；每次弃牌获得 1 点能量。
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class GaoDaYiHaoCanHun : ModRelicTemplate
{
    private const int HandSize = 4;
    private static readonly LocString SelectDiscardPrompt = new("relics", "YJCM_RELIC_GAO_DA_YI_HAO_CAN_HUN.select_discard");
    private bool _isReconcilingHand;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override string PackedIconPath => "res://Yjcm/Images/Relics/gao_da_yi_hao_can_hun.png";
    protected override string PackedIconOutlinePath => "res://Yjcm/Images/Relics/gao_da_yi_hao_can_hun.png";
    protected override string BigIconPath => "res://Yjcm/Images/Relics/Big/gao_da_yi_hao_can_hun.png";

    /// <summary>
    /// 任意牌堆变动后立即校正手牌数。此钩子本身没有选择上下文，
    /// 因而以 HookPlayerChoiceContext 将超量时的手动弃牌正确排入玩家动作队列。
    /// </summary>
    public override async Task AfterCardChangedPiles(
        CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (_isReconcilingHand || Owner?.Creature.CombatState == null || card.Owner != Owner)
        {
            await base.AfterCardChangedPiles(card, oldPileType, clonedBy);
            return;
        }

        var localPlayerId = LocalContext.NetId;
        if (!localPlayerId.HasValue)
        {
            await base.AfterCardChangedPiles(card, oldPileType, clonedBy);
            return;
        }

        // 同一批牌堆变动会并发通知每张牌。必须在 await 基类回调前占用锁，
        // 否则后续通知会在这里同时穿透，分别创建弃牌选择，导致重复弃牌。
        _isReconcilingHand = true;
        try
        {
            await base.AfterCardChangedPiles(card, oldPileType, clonedBy);
            var choiceContext = new HookPlayerChoiceContext(
                this, localPlayerId.Value, Owner.Creature.CombatState, GameActionType.Combat);
            await choiceContext.AssignTaskAndWaitForPauseOrCompletion(ReconcileHand(choiceContext));
        }
        catch
        {
            // 若动作入队本身失败，ReconcileHand 不会接管 finally，不能遗留锁定状态。
            _isReconcilingHand = false;
            throw;
        }
    }

    private async Task ReconcileHand(PlayerChoiceContext choiceContext)
    {
        try
        {
            while (true)
            {
                var hand = PileTypeExtensions.GetPile(PileType.Hand, Owner)?.Cards.ToList() ?? [];
                if (hand.Count <= HandSize)
                {
                    if (hand.Count < HandSize)
                        await CardPileCmd.Draw(choiceContext, HandSize - hand.Count, Owner, false);
                    return;
                }

                var excess = hand.Count - HandSize;
                var selected = (await CardSelectCmd.FromHandForDiscard(
                    choiceContext,
                    Owner,
                    new CardSelectorPrefs(SelectDiscardPrompt, excess, excess)
                    {
                        Cancelable = false,
                        RequireManualConfirmation = true
                    },
                    null,
                    this)).ToList();

                // 选择页面暂停期间，批量摸牌等同一动作仍可能继续向手牌加入牌。
                // 每轮结算后重新读取手牌并继续选择，直至严格回到四张。
                if (selected.Count != excess ||
                    selected.Distinct().Count() != excess ||
                    !selected.All(c => c.Owner == Owner && c.Pile?.Type == PileType.Hand))
                    continue;

                foreach (var card in selected)
                    await CardCmd.Discard(choiceContext, card);
            }
        }
        finally
        {
            _isReconcilingHand = false;
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);
        if (Owner?.Creature.CombatState == null || cardPlay.Card.Owner != Owner)
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, 6, ValueProp.Move, cardPlay);

        var enemies = Owner.Creature.CombatState.HittableEnemies.ToList();
        if (enemies.Count == 0)
            return;

        var target = Owner.RunState.Rng.CombatTargets.NextItem(enemies);
#if GAME_BETA
        await CreatureCmd.Damage(choiceContext, target, 6, ValueProp.Unpowered, cardPlay.Card, cardPlay);
#else
        await CreatureCmd.Damage(choiceContext, target, 6, ValueProp.Unpowered, Owner.Creature, cardPlay.Card);
#endif
    }

    public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
    {
        await base.AfterCardDiscarded(choiceContext, card);
        if (card.Owner != Owner)
            return;

        Flash();
        await PlayerCmd.GainEnergy(1, Owner);
    }
}
