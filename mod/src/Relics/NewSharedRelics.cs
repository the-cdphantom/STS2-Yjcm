using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable

namespace Yjcm;

/// <summary>本批共享遗物统一使用独立 PNG，同时将同一张透明图用于小图、描边图和大图。</summary>
public abstract class YjcmSharedRelic : ModRelicTemplate
{
    protected abstract string IconFileName { get; }

    public override string PackedIconPath =>
        $"res://Yjcm/Images/Relics/{IconFileName}";
    protected override string PackedIconOutlinePath =>
        $"res://Yjcm/Images/Relics/{IconFileName}";
    protected override string BigIconPath =>
        $"res://Yjcm/Images/Relics/Big/{IconFileName}";

    protected bool IsOwnersPoweredAttack(
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!props.IsPoweredAttack())
            return false;
        return dealer == Owner.Creature || cardSource?.Owner == Owner;
    }
}

/// <summary>龙印：战斗开始时对所有敌人施加6层火攻。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class LongYin : YjcmSharedRelic
{
    protected override string IconFileName => "long_yin.png";
    public override RelicRarity Rarity => RelicRarity.Common;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<HuoGongPower>()];

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        if (Owner?.Creature.CombatState == null)
            return;

        Flash();
        var context = new ThrowingPlayerChoiceContext();
        foreach (var enemy in Owner.Creature.CombatState.HittableEnemies.ToList())
            await PowerCmd.Apply<HuoGongPower>(
                context, enemy, 6, Owner.Creature, null, false);
    }
}

/// <summary>凤印：战斗开始时对所有敌人施加6层连环。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class FengYin : YjcmSharedRelic
{
    protected override string IconFileName => "feng_yin.png";
    public override RelicRarity Rarity => RelicRarity.Common;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<LianHuanPower>()];

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        if (Owner?.Creature.CombatState == null)
            return;

        Flash();
        var context = new ThrowingPlayerChoiceContext();
        foreach (var enemy in Owner.Creature.CombatState.HittableEnemies.ToList())
            await PowerCmd.Apply<LianHuanPower>(
                context, enemy, 6, Owner.Creature, null, false);
    }
}

/// <summary>孙皓的浴袍：持有者每弃置一张牌，随机对一名敌人造成2点伤害。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class SunHaoYuPao : YjcmSharedRelic
{
    protected override string IconFileName => "sun_hao_yu_pao.png";
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterCardDiscarded(
        PlayerChoiceContext choiceContext, CardModel card)
    {
        await base.AfterCardDiscarded(choiceContext, card);
        if (card.Owner != Owner || Owner?.Creature.CombatState == null)
            return;

        var enemies = Owner.Creature.CombatState.HittableEnemies.ToList();
        var target = Owner.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext, target, 2, ValueProp.Unpowered, null, null);
    }
}

/// <summary>陆逊的火把：对带有连环的敌人造成的攻击伤害提高25%。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class LuXunHuoBa : YjcmSharedRelic
{
    protected override string IconFileName => "lu_xun_huo_ba.png";
    public override RelicRarity Rarity => RelicRarity.Common;

    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props,
#if GAME_BETA
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#else
        Creature? dealer, CardModel? cardSource)
#endif
    {
        if (target == null || !IsOwnersPoweredAttack(props, dealer, cardSource))
            return 1m;
        if (target.Powers?.OfType<LianHuanPower>().Any(p => p.Amount > 0) != true)
            return 1m;
        return 1.25m;
    }
}

/// <summary>
/// 孔明灯：击败Boss后可精确删除四张牌并获得一个随机稀有遗物；取消选择则均不发生。
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class KongMingDeng : YjcmSharedRelic
{
    protected override string IconFileName => "kong_ming_deng.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        await base.AfterCombatVictory(room);
        if (room.RoomType != RoomType.Boss || Owner == null || Owner.Creature.IsDead)
            return;
        if (Owner.Deck.Cards.Count(card => card.IsRemovable) < 4)
            return;

        var selected = (await CardSelectCmd.FromDeckForRemoval(
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 4, 4)
            {
                Cancelable = true,
                // 即使恰好只剩四张可删牌，也必须展示原生选择界面，允许跳过。
                RequireManualConfirmation = true
            })).ToList();
        if (!CanExchangeCards(Owner, selected))
            return;

        Flash();
        foreach (var card in selected)
            await CardPileCmd.RemoveFromDeck(card);

        var relic = RelicFactory.PullNextRelicFromFront(Owner, RelicRarity.Rare).ToMutable();
        await RelicCmd.Obtain(relic, Owner);
    }

    /// <summary>在任何删牌或奖励副作用之前，完整校验原生联机选择返回的四张牌。</summary>
    internal static bool CanExchangeCards(Player owner, IReadOnlyList<CardModel> selected) =>
        selected.Count == 4 && selected.Distinct().Count() == 4 &&
        selected.All(card => card.Owner == owner && card.Pile?.Type == PileType.Deck && card.IsRemovable);
}

/// <summary>荀彧的香炉：高血量敌人对持有者造成未格挡伤害后，受到10点反伤。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class XunYuXiangLu : YjcmSharedRelic
{
    protected override string IconFileName => "xun_yu_xiang_lu.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        await base.AfterDamageReceived(
            choiceContext, target, result, props, dealer, cardSource);
        if (target != Owner.Creature || result.UnblockedDamage <= 0)
            return;
        if (dealer == null || dealer.IsDead || dealer.Side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy)
            return;
        if (dealer.CurrentHp <= target.CurrentHp)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext, dealer, 10, ValueProp.Unpowered, null, null);
    }
}

/// <summary>何进的屠刀：每次战斗胜利后额外获得10至30金币。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class HeJinTuDao : YjcmSharedRelic
{
    protected override string IconFileName => "he_jin_tu_dao.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        await base.AfterCombatVictory(room);
        if (Owner == null || Owner.Creature.IsDead)
            return;

        int gold = Owner.PlayerRng.Rewards.NextInt(10, 31);
        Flash();
        await PlayerCmd.GainGold(gold, Owner);
    }
}

/// <summary>华佗的药箱：战斗奖励生成时有25%概率追加一瓶随机药水。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class HuaTuoYaoXiang : YjcmSharedRelic
{
    protected override string IconFileName => "hua_tuo_yao_xiang.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override bool TryModifyRewards(
        MegaCrit.Sts2.Core.Entities.Players.Player player,
        List<Reward> rewards,
        AbstractRoom? room)
    {
        if (player != Owner || room is not CombatRoom)
            return false;
        if (player.PlayerRng.Rewards.NextDouble() >= 0.25)
            return false;

        Flash();
        rewards.Add(new PotionReward(player));
        return true;
    }
}

/// <summary>张鲁的布道幡：杀额外造成牌组中武将牌数量的伤害。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class ZhangLuBuDaoFan : YjcmSharedRelic
{
    protected override string IconFileName => "zhang_lu_bu_dao_fan.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override decimal ModifyDamageAdditive(
        Creature? target, decimal amount, ValueProp props,
#if GAME_BETA
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#else
        Creature? dealer, CardModel? cardSource)
#endif
        => GetShaBonus(IsMutable ? Owner : null, props, dealer, cardSource);

    internal static decimal GetShaBonus(Player? owner, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (owner == null || cardSource is not (Sha or WeiDongZhuoSha or WeiCaoCaoSha)
            || !props.IsPoweredAttack() || (dealer != owner.Creature && cardSource.Owner != owner))
            return 0m;
        return owner.Deck.Cards.Count(card => card.Tags.Contains(YjcmCardTags.WuJiang));
    }
}

/// <summary>张飞的鞭子：对当前血量低于持有者的敌人造成的攻击伤害提高50%。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class ZhangFeiBianZi : YjcmSharedRelic
{
    protected override string IconFileName => "zhang_fei_bian_zi.png";
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props,
#if GAME_BETA
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#else
        Creature? dealer, CardModel? cardSource)
#endif
    {
        if (target == null || !IsOwnersPoweredAttack(props, dealer, cardSource))
            return 1m;
        return target.CurrentHp < Owner.Creature.CurrentHp ? 1.5m : 1m;
    }
}

/// <summary>刘焉的诏令：事件专属遗物；使乐不思蜀必定击晕。</summary>
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class LiuYanZhaoLing : YjcmSharedRelic
{
    protected override string IconFileName => "liu_yan_zhao_ling.png";
    public override RelicRarity Rarity => RelicRarity.Event;
}
