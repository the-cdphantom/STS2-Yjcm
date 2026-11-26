using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Yjcm;

public abstract class WeiDongZhuoQunPower : ModPowerTemplate
{
    protected abstract string LocKey { get; }
    protected virtual string IconName => "li_sao_power";
    public override string CustomIconPath => $"res://WeiDongZhuo/images/{IconName}.png";
    public override string CustomBigIconPath => CustomIconPath;
    public override LocString Title => new("powers", $"YJCM_POWER_{LocKey}.title");
    public override LocString Description => new("powers", $"YJCM_POWER_{LocKey}.description");
}

public abstract class MoShouPowerBase : WeiDongZhuoQunPower
{
    protected abstract int CardsPerEnemy { get; }
    protected override string IconName => "mo_shou_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        await base.AfterPlayerTurnStart(context, player);
        if (player.Creature != Owner || Owner.IsDead) return;
        int attackers = Owner.CombatState.GetOpponentsOf(Owner)
            .Count(e => e.IsAlive && e.Monster?.IntendsToAttack == true);
        int count = attackers * CardsPerEnemy * Amount;
        if (count <= 0) return;
        Flash();
        WeiDongZhuoQunEffects.PlayVoice("mo_shou.wav");
        await CardPileCmd.AutoPlayFromDrawPile(context, player, count, CardPilePosition.Top, false);
    }
}

[RegisterPower]
public sealed class MoShouPower : MoShouPowerBase
{
    protected override int CardsPerEnemy => 1;
    protected override string LocKey => "MO_SHOU";
}

[RegisterPower]
public sealed class MoShouPlusPower : MoShouPowerBase
{
    // 兼容旧存档中的强化版能力 ID；新版本与普通墨守都只打出1张。
    protected override int CardsPerEnemy => 1;
    protected override string LocKey => "MO_SHOU_PLUS";
}

[RegisterPower]
public sealed class BaoNueXingTaiPower : WeiDongZhuoQunPower
{
    protected override string LocKey => "BAO_NUE_XING_TAI";
    protected override string IconName => "bao_nue_xing_tai_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

#if GAME_BETA
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
#endif
        // Damage remains decimal in the native pipeline, so an odd maximum HP grants
        // its exact half-point bonus instead of silently rounding down.
        => dealer == Owner && amount > 0 && !props.HasFlag(ValueProp.Unpowered)
            ? Owner.MaxHp * 0.5m * Amount
            : 0m;
}

// The base API has no max-HP-loss hook. Intercept its single state-changing
// command so direct SetMaxHp decreases and LoseMaxHp both trigger exactly once.
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp))]
internal static class BaoNueXingTaiMaxHpPatch
{
    private static void Prefix(Creature creature, out int __state) => __state = creature.MaxHp;

    private static void Postfix(Creature creature, int __state, ref Task<decimal> __result) =>
        __result = AwardEnergyAfterMaxHpLoss(__result, creature, __state);

    private static async Task<decimal> AwardEnergyAfterMaxHpLoss(Task<decimal> original, Creature creature, int oldMaxHp)
    {
        decimal changed = await original;
        if (creature.MaxHp >= oldMaxHp || creature.Player == null || creature.IsDead) return changed;

        foreach (var power in creature.Powers.OfType<BaoNueXingTaiPower>().Where(power => power.Amount > 0).ToArray())
        {
            await PlayerCmd.GainEnergy(power.Amount, creature.Player);
        }
        return changed;
    }
}

// 董卓的最大生命不能超过设定的硬上限；统一截断所有原生增减上限入口，
// 从而让遗物、卡牌、事件等效果的实际增量及对应历史记录保持一致。
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp))]
internal static class WeiDongZhuoMaxHpCapPatch
{
    internal const int Cap = 350234;

    private static void Prefix(Creature creature, ref decimal amount)
    {
        if (creature.Player?.Character is WeiDongZhuo)
            amount = Math.Min(amount, Cap);
    }
}

[RegisterPower]
public sealed class PoRongDoubleDamagePower : WeiDongZhuoQunPower
{
    protected override string LocKey => "PO_RONG_DOUBLE";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
#if GAME_BETA
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
#endif
        => dealer == Owner && cardSource != null && props.IsPoweredAttack() ? 2m : 1m;
}

[RegisterPower]
public sealed class ZhuiTaoPower : WeiDongZhuoQunPower
{
    protected override string LocKey => "ZHUI_TAO";
    protected override string IconName => "zhui_tao_buff";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        await base.AfterDamageGiven(context, dealer, result, props, target, cardSource);
        if (Owner.IsDead || target != Owner || result.TotalDamage <= 0) return;
        Flash();
        await PowerCmd.Apply<StrengthPower>(context, Owner, -Amount, dealer, cardSource, false);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEnd(context, side, participants);
        if (Applier != null && participants.Contains(Applier)) await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class QiPower : WeiDongZhuoQunPower
{
    protected override string LocKey => "QI";
    protected override string IconName => "qi_buff";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private async Task Trigger(PlayerChoiceContext context)
    {
        if (Owner.IsDead || Amount <= 0) return;
        Flash();
        await CreatureCmd.Damage(context, Owner, Amount * 6m, ValueProp.Move, null, null);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        await base.AfterCardPlayed(context, play);
        if (play.Card.Owner?.Creature == Applier && play.Card.Type == CardType.Attack)
            await Trigger(context);
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        await base.AfterDamageGiven(context, dealer, result, props, target, cardSource);
        if (dealer == Owner && target != Owner && result.TotalDamage > 0 && Owner.Monster?.IntendsToAttack == true)
            await Trigger(context);
    }
}

public abstract class YuXiPowerBase : WeiDongZhuoQunPower
{
    protected abstract bool FreeAttack { get; }
    protected override string IconName => "yu_xi_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private async Task Trigger(PlayerChoiceContext context)
    {
        if (Owner.Player == null || Owner.IsDead) return;
        Flash();
        for (int i = 0; i < Amount; i++)
        {
            var card = await CardPileCmd.Draw(context, Owner.Player);
            if (card?.Type != CardType.Attack) continue;
            if (FreeAttack) card.SetToFreeThisTurn();
            else card.EnergyCost.AddThisTurnOrUntilPlayed(-1, false);
        }
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        await base.AfterDamageGiven(context, dealer, result, props, target, cardSource);
        if (dealer == Owner && target != Owner && result.TotalDamage > 0) await Trigger(context);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        await base.AfterDamageReceived(context, target, result, props, dealer, cardSource);
        if (target == Owner && result.TotalDamage > 0) await Trigger(context);
    }
}

[RegisterPower]
public sealed class YuXiPower : YuXiPowerBase
{
    protected override bool FreeAttack => false;
    protected override string LocKey => "YU_XI";
}

[RegisterPower]
public sealed class YuXiPlusPower : YuXiPowerBase
{
    protected override bool FreeAttack => true;
    protected override string LocKey => "YU_XI_PLUS";
}

[RegisterPower]
public sealed class DuanTiPower : WeiDongZhuoQunPower
{
    private int _cardsPlayed;
    protected override string LocKey => "DUAN_TI";
    protected override string IconName => "duan_ti_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        await base.AfterCardPlayed(context, play);
        if (play.Card.Owner.Creature != Owner || play.Card is WeiDongZhuoWuPuDuanTi) return;
        _cardsPlayed++;
        if (_cardsPlayed < 5) return;
        _cardsPlayed -= 5;
        Flash();
        decimal gained = await CreatureCmd.SetMaxHp(Owner, Owner.MaxHp + Amount);
        var history = Owner.Player?.RunState.CurrentMapPointHistoryEntry;
        if (history != null && Owner.Player != null)
            history.GetEntry(Owner.Player.NetId).MaxHpGained += (int)gained;
        await CreatureCmd.Heal(Owner, Amount);
    }
}

[RegisterPower]
public sealed class XiongHuoPower : WeiDongZhuoQunPower
{
    protected override string LocKey => "XIONG_HUO";
    protected override string IconName => "xiong_huo_buff";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

#if GAME_BETA
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
#endif
        => target == Owner && amount > 0 ? Amount * 3m : 0m;

    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState state)
    {
        await base.BeforeSideTurnStart(context, side, participants, state);
        if (!participants.Contains(Owner) || Owner.IsDead) return;
        int amount = Amount;
        Flash();
        await PowerCmd.Remove(this);
        for (int i = 0; i < amount && Owner.IsAlive; i++)
        {
            switch (state.RunState.Rng.CombatCardSelection.NextInt(3))
            {
                case 0:
                    await CreatureCmd.Damage(context, Owner, 8, ValueProp.Move, null, null);
                    break;
                case 1:
                    await WeiDongZhuoQunEffects.LoseHp(context, Owner, 4);
                    if (Owner.IsAlive)
                        await PowerCmd.Apply<WeakPower>(context, Owner, 1, Applier, null, false);
                    break;
                default:
                    await PowerCmd.Apply<LiJiePower>(context, Owner, 2, Applier, null, false);
                    break;
            }
        }
    }
}

/// <summary>
/// 回合开始名单在凶镬结算前已拍快照；凶镬致死的敌人仍可能留在该名单。
/// 死者无须清格挡，避免对已移除的战斗监听者调用清格挡钩子而中断回合循环。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.AfterTurnStart))]
internal static class XiongHuoDeadCreatureTurnStartPatch
{
    [HarmonyPrefix]
    private static bool SkipDeadCreature(Creature __instance, ref Task __result)
    {
        if (!__instance.IsDead) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockCleared))]
internal static class XiongHuoDeadCreatureBlockClearedPatch
{
    [HarmonyPrefix]
    private static bool SkipDeadCreature(Creature creature, ref Task __result)
    {
        if (!creature.IsDead) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[RegisterPower]
public sealed class JianDaoPower : WeiDongZhuoQunPower
{
    private int _attackCardsPlayed;
    protected override string LocKey => "JIAN_DAO";
    protected override string IconName => "wang_yue_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        await base.AfterCardPlayed(context, play);
        if (play.Card.Owner?.Creature != Owner || play.Card.Type != CardType.Attack) return;
        _attackCardsPlayed++;
        while (_attackCardsPlayed >= 2 && Owner.IsAlive)
        {
            _attackCardsPlayed -= 2;
            Flash();
            await PowerCmd.Apply<StrengthPower>(context, Owner, Amount, Owner, play.Card, false);
            if (Owner.Player == null) continue;
            var card = Owner.CombatState.CreateCard<WeiDongZhuoWuSeSha>(Owner.Player);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner.Player, CardPilePosition.Random);
        }
    }
}

public abstract class ShiFengPowerBase : WeiDongZhuoQunPower
{
    protected abstract bool UpgradeDuel { get; }
    protected override string IconName => "wang_yue_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        await base.AfterDamageReceived(context, target, result, props, dealer, cardSource);
        if (target != Owner || result.UnblockedDamage <= 0 || Owner.Player == null) return;
        Flash();
        var cards = new List<CardModel>();
        for (int i = 0; i < Amount; i++)
        {
            var duel = Owner.CombatState.CreateCard<WeiDongZhuoJueDou>(Owner.Player);
            duel.SetToFreeThisCombat();
            if (UpgradeDuel && duel.IsUpgradable) duel.UpgradeInternal();
            cards.Add(duel);
        }
        await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, Owner.Player, CardPilePosition.Random);
    }
}

[RegisterPower]
public sealed class ShiFengPower : ShiFengPowerBase
{
    protected override bool UpgradeDuel => false;
    protected override string LocKey => "SHI_FENG";
}

[RegisterPower]
public sealed class ShiFengPlusPower : ShiFengPowerBase
{
    protected override bool UpgradeDuel => true;
    protected override string LocKey => "SHI_FENG_PLUS";
}

[RegisterPower]
public sealed class MoYouPower : WeiDongZhuoQunPower
{
    private int _cardsPlayed;
    protected override string LocKey => "MO_YOU";
    protected override string IconName => "mo_you_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        await base.AfterCardPlayed(context, play);
        if (play.Card.Owner.Creature != Owner || play.Card is WeiDongZhuoMouXuYouMoYou || Owner.Player == null) return;
        _cardsPlayed++;
        if (_cardsPlayed < 2) return;
        _cardsPlayed -= 2;
        Flash();
        await CardPileCmd.Draw(context, 3, Owner.Player);
        var hand = PileType.Hand.GetPile(Owner.Player);
        if (hand.Cards.Count == 0) return;
        var selected = (await CardSelectCmd.FromHandForDiscard(context, Owner.Player,
            WeiDongZhuoQunEffects.Prefs("YJCM_CARD_MO_YOU.select_prompt", 0, 1),
            _ => true, this)).FirstOrDefault();
        if (selected == null) return;
        var sameType = hand.Cards.Where(card => card.Type == selected.Type).ToList();
        if (sameType.Count == 0) return;
        await CardCmd.Discard(context, sameType);
        // 漠猷叠层只增加这次免费牌的张数；每两张牌仍只摸一次、弃置一次。
        await PowerCmd.Apply<MoYouFreeNextPower>(context, Owner, Amount, Owner, null, false);
    }
}

public abstract class MoYouFreePowerBase : WeiDongZhuoQunPower
{
    protected abstract bool CanMakeFree(CardModel card);
    protected override string IconName => "mo_you_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card.Owner?.Creature == Owner && CanMakeFree(card) && Amount > 0)
        {
            modifiedCost = 0;
            return true;
        }
        modifiedCost = originalCost;
        return false;
    }
    public override async Task BeforeCardPlayed(CardPlay play)
    {
        await base.BeforeCardPlayed(play);
        if (play.Card.Owner?.Creature == Owner && CanMakeFree(play.Card)) await PowerCmd.Decrement(this);
    }
}

[RegisterPower]
public sealed class MoYouFreeAttackPower : MoYouFreePowerBase
{
    protected override bool CanMakeFree(CardModel card) => card.Type == CardType.Attack;
    protected override string LocKey => "MO_YOU_FREE_ATTACK";
}

[RegisterPower]
public sealed class MoYouFreePowerCardPower : MoYouFreePowerBase
{
    protected override bool CanMakeFree(CardModel card) => card.Type == CardType.Skill;
    protected override string LocKey => "MO_YOU_FREE_POWER";
}

[RegisterPower]
public sealed class MoYouFreeNextPower : MoYouFreePowerBase
{
    protected override bool CanMakeFree(CardModel card) => true;
    protected override string LocKey => "MO_YOU_FREE_NEXT";
}

[RegisterPower]
public sealed class LianJiePower : WeiDongZhuoQunPower
{
    private CardModel? _pendingMinimumCostCard;
    private bool _usedThisTurn;

    protected override string LocKey => "LIAN_JIE";
    protected override string IconName => "lian_jie_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        await base.AfterPlayerTurnStart(context, player);
        if (player.Creature != Owner) return;
        _pendingMinimumCostCard = null;
        _usedThisTurn = false;
    }

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        await base.BeforeCardPlayed(play);
        if (_usedThisTurn || _pendingMinimumCostCard != null || Owner.Player == null ||
            play.Card.Owner?.Creature != Owner || play.Card is WeiDongZhuoWuHuangFuSongLianJie)
            return;

        var hand = PileType.Hand.GetPile(Owner.Player);
        // BeforeCardPlayed 时原生流程有时已先将这张牌移出手牌。将它显式补回比较集合，
        // 才能正确处理费用相同的多张牌和手牌只剩这一张的情况。
        var cardsAtPlayTime = hand.Cards.Contains(play.Card)
            ? hand.Cards
            : hand.Cards.Append(play.Card);
        // 诅咒、状态等不能打出的无费用牌不参与最低费用比较，避免把连捷锁死。
        var costedCards = cardsAtPlayTime
            .Where(card => card.Type is not CardType.Curse and not CardType.Status)
            .ToList();
        if (costedCards.Count == 0) return;
        int minimumCost = costedCards.Min(card => card.EnergyCost.GetWithModifiers(CostModifiers.All));
        if (play.Card.EnergyCost.GetWithModifiers(CostModifiers.All) == minimumCost)
            _pendingMinimumCostCard = play.Card;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        await base.AfterCardPlayed(context, play);
        if (!ReferenceEquals(_pendingMinimumCostCard, play.Card)) return;
        _pendingMinimumCostCard = null;
        if (_usedThisTurn || Owner.Player == null || Owner.IsDead) return;

        var hand = PileType.Hand.GetPile(Owner.Player);
        if (hand.Cards.Count == 0) return;
        var selected = (await CardSelectCmd.FromHand(context, Owner.Player,
            WeiDongZhuoQunEffects.Prefs("YJCM_CARD_LIAN_JIE.select_prompt", 0, 1),
            _ => true, this)).FirstOrDefault();
        if (selected == null) return;

        await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Bottom);
        int toDraw = Math.Max(0, CardPile.MaxCardsInHand - hand.Cards.Count);
        if (toDraw > 0)
        {
            var drawn = (await CardPileCmd.Draw(context, toDraw, Owner.Player, false)).ToList();
            foreach (var card in drawn.Where(card => card.Pile?.Type == PileType.Hand))
                card.SetToFreeThisTurn();
        }
        _usedThisTurn = true;
        Flash();
    }
}

[RegisterPower]
public sealed class JiangXianPower : WeiDongZhuoQunPower
{
    protected override string LocKey => "JIANG_XIAN";
    protected override string IconName => "jiang_xian_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        await base.AfterDamageGiven(context, dealer, result, props, target, cardSource);
        if (dealer != Owner || result.TotalDamage <= 0) return;
        Flash();
        await PowerCmd.Apply<StrengthPower>(context, Owner, Amount, Owner, cardSource, false);
    }
}

[RegisterPower]
public sealed class QuXianPower : WeiDongZhuoQunPower
{
    protected override string LocKey => "QU_XIAN";
    protected override string IconName => "qu_xian_buff";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private async Task AddCards()
    {
        if (Owner.Player == null || Owner.IsDead) return;
        var cards = new List<CardModel>();
        for (int i = 0; i < Amount; i++)
        {
            var card = Owner.CombatState.CreateCard<WeiDongZhuoWuSeSha>(Owner.Player);
            card.SetToFreeThisTurn();
            cards.Add(card);
        }
        await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, Owner.Player, CardPilePosition.Random);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        await base.AfterPlayerTurnStart(context, player);
        if (player.Creature == Owner) { Flash(); await AddCards(); }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEnd(context, side, participants);
        if (participants.Contains(Owner)) { Flash(); await AddCards(); }
    }
}

[RegisterPower]
public sealed class NeiFaPower : WeiDongZhuoQunPower
{
    [SavedProperty]
    public int BlockedCardType { get; set; } = -1;
    [SavedProperty]
    public int FreeCardType { get; set; } = -1;
    [SavedProperty]
    public int RemainingFreePlays { get; set; }

    protected override string LocKey => "NEI_FA";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override int DisplayAmount => RemainingFreePlays;
    public override LocString Description
    {
        get
        {
            if (BlockedCardType < 0 || FreeCardType < 0) return base.Description;
            var description = new LocString("powers", "YJCM_POWER_NEI_FA.active_description");
            description.Add("BlocksSkills", BlockedCardType == (int)CardType.Skill);
            description.Add("Remaining", RemainingFreePlays);
            return description;
        }
    }

    internal static int CountBlockedCards(IEnumerable<CardModel> hand, CardType blocked) =>
        hand.Count(card => card.Type == blocked);

    public void Configure(CardType blocked, CardType free, int freePlays)
    {
        AssertMutable();
        BlockedCardType = (int)blocked;
        FreeCardType = (int)free;
        RemainingFreePlays = Math.Max(0, freePlays);
        InvokeDisplayAmountChanged();
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        card.Owner?.Creature != Owner || (int)card.Type != BlockedCardType;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (RemainingFreePlays > 0 && card.Owner?.Creature == Owner && (int)card.Type == FreeCardType)
        {
            modifiedCost = 0;
            return true;
        }
        modifiedCost = originalCost;
        return false;
    }

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        await base.BeforeCardPlayed(play);
        // Costs are already paid. Reserve this use before nested plays; Replay is one paid play.
        if (play.IsFirstInSeries && RemainingFreePlays > 0 && play.Card.Owner?.Creature == Owner &&
            (int)play.Card.Type == FreeCardType)
        {
            RemainingFreePlays--;
            InvokeDisplayAmountChanged();
        }
        // Do not remove at zero: the opposite card type remains blocked until turn end.
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEnd(context, side, participants);
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}
