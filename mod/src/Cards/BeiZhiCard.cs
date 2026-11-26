using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

using STS2RitsuLib.Interop.AutoRegistration;

using Yjcm.Extensions;

#nullable enable

namespace Yjcm;

/// <summary>
/// 背置牌：0费技能牌，消耗。每回合每张限一次，
/// 选择任意一张牌（所有角色含 mod 卡）的副本加入手牌。
/// 卡面：bei_zhi.png（显式指定；基类按 Id 推断会去找 bei_zhi_card.png）。
/// </summary>
[RegisterCard(typeof(TokenCardPool))]
public sealed class BeiZhiCard : WuZhugeliangCard
{
    /// <summary>每回合每张牌限一次标记</summary>
    private bool _usedThisTurn;

    public BeiZhiCard() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false) { }
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>卡面：背置牌.png（RitsuLib 保留 Card 后缀，显式指向 bei_zhi.png）</summary>
    public override string CustomPortraitPath => "bei_zhi.png".CardImagePath();

    /// <summary>消耗：打出后移出游戏</summary>
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 每回合每张限一次
        if (_usedThisTurn) return;
        _usedThisTurn = true;

        // 候选 = 全卡池（含所有角色与 mod 卡），全部放进选择页实现真·任意选（实测全量不卡顿）
        var candidates = CardSelectionHelper.GetEligibleCards(includeAttacks: true, exclude: this);
        if (candidates.Count == 0) return;

        var options = CardFactory.GetDistinctForCombat(
            Owner, candidates, candidates.Count,
            Owner.RunState.Rng.CombatCardGeneration).ToList();
        if (options.Count == 0) return;

        // 开启选牌搜索框（仅本次选牌生效），结束后复位
        SearchableCardSelectPatch.SearchEnabled = true;
        CardModel? chosen;
        try
        {
            chosen = (await CardSelectCmd.FromSimpleGrid(
                choiceContext, options, Owner,
                new CardSelectorPrefs(
                    new MegaCrit.Sts2.Core.Localization.LocString("cards", "beizhi.select_prompt"),
                    1, 1))).FirstOrDefault();
        }
        finally
        {
            SearchableCardSelectPatch.SearchEnabled = false;
        }
        if (chosen == null) return; // 取消 → 背置牌仍消耗

        // 将选中的牌（战斗副本）加入手牌
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner, CardPilePosition.Random);
    }

    /// <summary>每回合重置限用标记</summary>
    public override async Task AfterSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        if (side == CombatSide.Player) _usedThisTurn = false;
    }

    protected override void OnUpgrade() { }
}
