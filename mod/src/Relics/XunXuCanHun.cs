using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;

#nullable enable

namespace Yjcm;

/// <summary>
/// 先古之民遗物「荀勖残魂」：Ancient 稀有度。
/// 每有七张牌进入弃牌堆时，下一次进入弃牌堆的牌返回手牌（返回一次后重置计数，重新累计）；
/// 右键此遗物：弃置三张手牌（玩家选3），再选任意一张非攻击牌加入手牌，
/// 使其变为 0 费并附加虚无、消耗；同一种牌每回合最多选择一次。
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class XunXuCanHun : ModRelicTemplate, IModRightClickableRelic
{
    /// <summary>累计进弃牌堆计数（跨回合累计，满 7 触发返回）</summary>
    private int _discardCount;
    /// <summary>返回状态：为 true 时下一次进弃牌堆的牌返回手牌，返回后立即重置</summary>
    private bool _pendingReturn;
    /// <summary>本回合已经印过的牌模型 ID；同一种牌每回合最多印一次</summary>
    private readonly HashSet<string> _imprintedCardIdsThisTurn = new(StringComparer.Ordinal);

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override string PackedIconPath =>
        "res://Yjcm/Images/Relics/xun_xu_can_hun.png";
    protected override string PackedIconOutlinePath =>
        "res://Yjcm/Images/Relics/xun_xu_can_hun.png";
    protected override string BigIconPath =>
        "res://Yjcm/Images/Relics/Big/xun_xu_can_hun.png";

    public override Task BeforeCombatStart()
    {
        _discardCount = 0;
        _pendingReturn = false;
        _imprintedCardIdsThisTurn.Clear();
        return base.BeforeCombatStart();
    }

    /// <summary>牌进入弃牌堆：命中返回批则回手；否则累计计数，满 7 置返回批</summary>
    public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
    {
        await base.AfterCardDiscarded(choiceContext, card);
        if (card.Owner != Owner) return; // 多人：只统计自己弃的牌

        if (_pendingReturn)
        {
            // 返回一次后立即重置，重新累计 7 张才能再次触发返回
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Random, this, false);
            _pendingReturn = false;
            _discardCount = 0;
            return;
        }

        _discardCount++;
        if (_discardCount < 7) return;
        _discardCount = 0;
        _pendingReturn = true;
    }

    /// <summary>玩家回合开始：兜底重置（触发后本回合未返回则清掉状态）</summary>
    public override async Task AfterSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        if (side == CombatSide.Player)
        {
            _pendingReturn = false;
            _imprintedCardIdsThisTurn.Clear();
        }
    }

    /// <summary>右键：弃三张手牌，选一张非攻击牌入手（0费+虚无+消耗）</summary>
    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        var player = context.Player;
        if (player == null || player.Creature.CombatState == null) return; // 仅战斗内可用
        var ctx = context.PlayerChoiceContext;
        if (ctx == null) return;

        // 手牌不足 3 张无法执行
        var handCount = PileTypeExtensions.GetPile(PileType.Hand, player)?.Cards.Count ?? 0;
        if (handCount < 3) return;

        // 先记录玩家选择，第二次选择结束前不改动战斗状态。
        // 多人模式允许队友在本动作等待选择时继续行动；若此处提前弃牌，
        // 两端会在不同时间获得战术大师等弃牌效果并触发状态分歧。
        var toDiscard = (await CardSelectCmd.FromHandForDiscard(
            ctx, player,
            new CardSelectorPrefs(
                new MegaCrit.Sts2.Core.Localization.LocString("cards", "xun_xu.discard_prompt"),
                3, 3),
            null, this)).ToList();
        if (toDiscard.Count < 3) return; // 取消则无事发生

        // 选择页使用不属于战斗状态的预览副本，并按模型 ID 固定顺序。
        // 禁止在选择前调用 GetDistinctForCombat：它会为全部候选推进全局战斗 RNG，
        // 队友若同时结算卡牌，主机与客户端会在不同的 RNG 进度上执行。
        var candidates = CardSelectionHelper.GetEligibleCards(includeAttacks: false, exclude: null)
            .Where(c => !_imprintedCardIdsThisTurn.Contains(c.Id.Entry))
            .OrderBy(c => c.Id.Entry, StringComparer.Ordinal)
            .ToList();
        if (candidates.Count == 0) return;
        var options = candidates.Select(c =>
        {
            var preview = c.ToMutable();
            preview.Owner = player;
            return preview;
        }).ToList();
        if (options.Count == 0) return;

        // 开启选牌搜索框（仅本次选牌生效），结束后复位
        SearchableCardSelectPatch.SearchEnabled = true;
        CardModel? chosen;
        try
        {
            chosen = (await CardSelectCmd.FromSimpleGrid(
                ctx, options, player,
                new CardSelectorPrefs(
                    new MegaCrit.Sts2.Core.Localization.LocString("cards", "xun_xu.select_prompt"),
                    1, 1))).FirstOrDefault();
        }
        finally
        {
            SearchableCardSelectPatch.SearchEnabled = false;
        }
        if (chosen == null) return;

        // 两次选择均结束后再一次性结算；若期间选中的手牌已被其他效果移走，则取消本次效果。
        if (player.Creature.CombatState == null || player.Creature.IsDead ||
            toDiscard.Any(c => c.Owner != player || c.Pile?.Type != PileType.Hand))
            return;

        // 选择页打开期间可能发生重复点击或多人异步结算；最终结算前再次登记，
        // 若同一种牌已被本回合另一笔印牌占用，则本次不弃牌也不生成牌。
        if (!_imprintedCardIdsThisTurn.Add(chosen.Id.Entry)) return;

        foreach (var c in toDiscard)
            await CardCmd.Discard(ctx, c);

        // 只在最终结算时创建所选牌的战斗实例，不再为整页候选推进战斗 RNG。
        var canonical = ModelDb.GetById<CardModel>(chosen.Id);
        var generated = player.Creature.CombatState.CreateCard(canonical, player);
        generated.SetToFreeThisCombat();
        CardCmd.ApplyKeyword(generated, CardKeyword.Ethereal);
        CardCmd.ApplyKeyword(generated, CardKeyword.Exhaust);
        await CardPileCmd.AddGeneratedCardToCombat(
            generated, PileType.Hand, player, CardPilePosition.Bottom);
    }
}
