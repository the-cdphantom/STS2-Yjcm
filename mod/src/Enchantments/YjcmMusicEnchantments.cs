using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Yjcm;

/// <summary>音律附魔共有的保留与一次性消耗处理。</summary>
public abstract class YjcmMusicEnchantment : ModEnchantmentTemplate
{
    [SavedProperty]
    private bool AddedRetain { get; set; }

    protected abstract string EnchantmentIconPath { get; }
    public override EnchantmentAssetProfile AssetProfile => new(EnchantmentIconPath);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromKeyword(CardKeyword.Retain)];

    protected override void OnEnchant()
    {
        // 已由此附魔赋予保留（包括读档后）时不重复处理；卡本身已有保留也不能被消耗逻辑移除。
        if (AddedRetain || Card.Keywords.Contains(CardKeyword.Retain)) return;

        AddedRetain = true;
        Card.AddKeyword(CardKeyword.Retain);
    }

    protected void ConsumeEnchantment()
    {
        var enchantedCard = Card;
        var removeRetain = AddedRetain;
        CardCmd.ClearEnchantment(enchantedCard);
        if (removeRetain)
            CardCmd.RemoveKeyword(enchantedCard, CardKeyword.Retain);
    }
}

/// <summary>檀板：打出后可把另一张未附魔檀板的手牌免费打出一次。</summary>
[RegisterEnchantment]
public sealed class TanBan : YjcmMusicEnchantment
{
    protected override string EnchantmentIconPath => "res://WeiDongZhuo/images/tan_ban_enchantment.png";
    private static readonly LocString SelectCardPrompt =
        new("enchantments", "YJCM_ENCHANTMENT_TAN_BAN.select_prompt");

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);
        if (Status != EnchantmentStatus.Normal || cardPlay.Card != Card) return;

        var enchantedCard = Card;
        var owner = enchantedCard.Owner;
        if (CombatManager.Instance.IsOverOrEnding || owner.Creature.IsDead) return;
        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            owner,
            new CardSelectorPrefs(SelectCardPrompt, 0, 1),
            card => card != enchantedCard && card.Enchantment is not TanBan &&
                    !card.Keywords.Contains(CardKeyword.Unplayable),
            null)).FirstOrDefault();

        // 原牌一经打出，檀板即失效；即使玩家跳过或取消目标选择亦不保留。
        ConsumeEnchantment();
        if (selected == null || !CanAutoPlay(selected)) return;

        Creature? target = null;
        if (TanBanTargetSelection.RequiresManualTarget(selected.TargetType))
        {
            target = await TanBanTargetSelection.SelectTargetAsync(choiceContext, selected);
            if (target == null) return;
        }

        if (!CanAutoPlay(selected) || (target != null &&
            (owner.Creature.CombatState?.Creatures.Contains(target) != true || !selected.IsValidTarget(target)))) return;
        // AutoPlay 本身不消耗费用；取消选择不能改变该手牌之后的正常费用。
        await CardCmd.AutoPlay(choiceContext, selected, target, AutoPlayType.Default, false, false);
    }

    internal static bool CanAutoPlay(CardModel card) =>
        !CombatManager.Instance.IsOverOrEnding && !card.Owner.Creature.IsDead &&
        card.Pile?.Type == PileType.Hand && !card.Keywords.Contains(CardKeyword.Unplayable);
}

/// <summary>胡笳：回合末在手时给格挡，弃置时按不同费用生成临时牌。</summary>
[RegisterEnchantment]
public sealed class HuJia : YjcmMusicEnchantment
{
    private const int BlockAmount = 5;
    protected override string EnchantmentIconPath => "res://WeiDongZhuo/images/hu_jia_enchantment.png";

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        await base.BeforeSideTurnEnd(choiceContext, side, participants);
        if (Status != EnchantmentStatus.Normal || side != CombatSide.Player) return;
        if (Card.Pile?.Type != PileType.Hand || !participants.Contains(Card.Owner.Creature)) return;

        await CreatureCmd.GainBlock(Card.Owner.Creature, BlockAmount, ValueProp.Unpowered, null);
    }

    public override async Task AfterCardChangedPiles(
        CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        await base.AfterCardChangedPiles(card, oldPileType, clonedBy);
        if (Status != EnchantmentStatus.Normal || card != Card || card.Pile?.Type != PileType.Discard)
            return;

        var owner = card.Owner;
        var excludedCost = card.EnergyCost.GetResolved();
        var choiceContext = new ThrowingPlayerChoiceContext();
        var candidates = CardSelectionHelper.GetEligibleCards(includeAttacks: true, exclude: null);

        for (var cost = 0; cost <= 3; cost++)
        {
            if (cost == excludedCost) continue;

            var matchingCards = candidates
                .Where(candidate => !candidate.EnergyCost.CostsX && candidate.EnergyCost.Canonical == cost)
                .ToList();
            if (matchingCards.Count == 0) continue;

            var generated = CardFactory.GetDistinctForCombat(
                owner, matchingCards, 1, owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
            if (generated == null) continue;

            generated.SetToFreeThisTurn();
            CardCmd.ApplyKeyword(generated, CardKeyword.Ethereal);
            CardCmd.ApplyKeyword(generated, CardKeyword.Exhaust);
            await CardPileCmd.AddGeneratedCardToCombat(
                generated, PileType.Hand, owner, CardPilePosition.Random);
        }

        ConsumeEnchantment();
    }
}

/// <summary>弦：弃置时获得一层缓冲。</summary>
[RegisterEnchantment]
public sealed class JiaoWei : YjcmMusicEnchantment
{
    protected override string EnchantmentIconPath => "res://WeiDongZhuo/images/jiao_wei_enchantment.png";
    public override async Task AfterCardChangedPiles(
        CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        await base.AfterCardChangedPiles(card, oldPileType, clonedBy);
        if (Status != EnchantmentStatus.Normal || card != Card || card.Pile?.Type != PileType.Discard)
            return;

        await PowerCmd.Apply<BufferPower>(
            new ThrowingPlayerChoiceContext(), card.Owner.Creature, 1, card.Owner.Creature, card, false);
        ConsumeEnchantment();
    }
}
