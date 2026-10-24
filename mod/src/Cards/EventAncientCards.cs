using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;
using Yjcm.Extensions;

#nullable enable

namespace Yjcm;

internal static class EventAncientEffects
{
    internal static string Voice(string file) => Path.Combine(
        Path.GetDirectoryName(typeof(EventAncientEffects).Assembly.Location)!,
        "Voice", "Bank", "Assets", file);

    // This is intentionally source-less special HP loss: it bypasses block and all damage modifiers.
    internal static Task LoseHp(PlayerChoiceContext context, Creature creature, decimal amount) =>
        CreatureCmd.Damage(context, creature, amount,
            ValueProp.Unblockable | ValueProp.Unpowered, null, null);
}

/// <summary>势魏延-壮誓：事件获得的先古能力。</summary>
[RegisterCard(typeof(TokenCardPool))]
public sealed class ShiWeiYanZhuangShi : WuZhugeliangCard
{
    public ShiWeiYanZhuangShi() : base(3, CardType.Power, CardRarity.Ancient, TargetType.Self) { }
    public override bool CanBeGeneratedInCombat => false;
    public override string CustomPortraitPath => "shi_wei_yan_zhuang_shi.png".CardImagePath();
    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Shu];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<KuangGuPower>()];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        VoicePlayer.PlayWav(EventAncientEffects.Voice("shi_wei_yan_zhuang_shi.wav"));
        await PowerCmd.Apply<ZhuangShiPreparationPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
        await PowerCmd.Apply<KuangGuPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
        _ = ShiWeiYanVfx.Play(ShiWeiYanVfxStyle.ZhuangShi);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        AddKeyword(CardKeyword.Innate);
    }
}

/// <summary>势魏延-壮誓-傲：忠傲任务完成后替换壮誓。</summary>
[RegisterCard(typeof(TokenCardPool))]
public sealed class ShiWeiYanZhuangShiAo : WuZhugeliangCard
{
    public ShiWeiYanZhuangShiAo() : base(3, CardType.Power, CardRarity.Ancient, TargetType.Self) { }
    public override bool CanBeGeneratedInCombat => false;
    public override string CustomPortraitPath => "shi_wei_yan_zhuang_shi_ao.png".CardImagePath();
    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Shu];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<KuangGuAoPower>()];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        VoicePlayer.PlayWav(EventAncientEffects.Voice("shi_wei_yan_zhuang_shi_ao.wav"));
        await PowerCmd.Apply<ZhuangShiAoPreparationPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
        await PowerCmd.Apply<KuangGuAoPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
        _ = ShiWeiYanVfx.Play(ShiWeiYanVfxStyle.ZhuangShiAo);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        AddKeyword(CardKeyword.Innate);
    }
}

/// <summary>势魏延-忠傲：进入牌组并跨战斗计数的任务牌。</summary>
[RegisterCard(typeof(QuestCardPool))]
public sealed class ShiWeiYanZhongAo : WuZhugeliangCard
{
    private const string CombatsKey = "Combats";
    private const int RequiredCombats = 5;
    private int _combatsSeen;

    public ShiWeiYanZhongAo() : base(-1, CardType.Quest, CardRarity.Quest, TargetType.None) { }
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    public override string CustomPortraitPath => "shi_wei_yan_zhong_ao.png".CardImagePath();
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(CombatsKey, RequiredCombats)];

    [SavedProperty]
    public int CombatsSeen
    {
        get => _combatsSeen;
        set
        {
            AssertMutable();
            _combatsSeen = value;
            DynamicVars[CombatsKey].BaseValue = Math.Max(0, RequiredCombats - _combatsSeen);
        }
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await base.AfterCombatEnd(room);
        if (Pile?.Type != PileType.Deck) return;

        CombatsSeen++;
        if (CombatsSeen < RequiredCombats || Pile?.Type != PileType.Deck) return;

        // A quest card progresses alongside the deck it belongs to; one completed quest upgrades one 壮誓.
        var zhuangShi = Pile.Cards.OfType<ShiWeiYanZhuangShi>().FirstOrDefault();
        if (zhuangShi != null)
        {
            var replacement = zhuangShi.CardScope.CreateCard<ShiWeiYanZhuangShiAo>(zhuangShi.Owner);
            if (zhuangShi.IsUpgraded) CardCmd.Upgrade(replacement);
            await CardCmd.Transform(zhuangShi, replacement);
            VoicePlayer.PlayWav(EventAncientEffects.Voice("shi_wei_yan_zhong_ao.wav"));
            _ = ShiWeiYanVfx.Play(ShiWeiYanVfxStyle.ZhongAoCompleted);
        }

        if (Pile?.Type == PileType.Deck) await CardPileCmd.RemoveFromDeck(this);
    }
}

/// <summary>谋诸葛亮-火计：事件获得的先古攻击。</summary>
[RegisterCard(typeof(TokenCardPool))]
public sealed class MouZhugeliangHuoJi : WuZhugeliangCard
{
    private const string HuoGongKey = "HuoGong";

    public MouZhugeliangHuoJi() : base(1, CardType.Attack, CardRarity.Ancient, TargetType.AnyEnemy) { }
    public override bool CanBeGeneratedInCombat => false;
    public override string CustomPortraitPath => "mou_zhugeliang_huo_ji.png".CardImagePath();
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Shu];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new HuoGongVar(HuoGongKey, 10m)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<HuoGongPower>(), HoverTipFactory.FromPower<BufferPower>()];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        VoicePlayer.PlayWav(EventAncientEffects.Voice("mou_zhugeliang_huo_ji.wav"));
        var target = play.Target!;
#if GAME_BETA
        if (target.Block > 0) await CreatureCmd.LoseBlock(context, target, target.Block, null);
#else
        if (target.Block > 0) await CreatureCmd.LoseBlock(target, target.Block);
#endif
        foreach (var artifact in target.Powers.OfType<ArtifactPower>().ToArray())
            await PowerCmd.Remove(artifact);
        await PowerCmd.Apply<HuoGongPower>(context, target, DynamicVars[HuoGongKey].BaseValue,
            Owner.Creature, this, false);
        if (target.Monster?.IntendsToAttack == true)
            await PowerCmd.Apply<BufferPower>(context, Owner.Creature, 2, Owner.Creature, this, false);
        var kongCheng = CombatState.CreateCard<MouZhugeliangKongCheng>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(kongCheng, PileType.Draw, Owner, CardPilePosition.Random);
    }

    protected override void OnUpgrade() => DynamicVars[HuoGongKey].UpgradeValueBy(5);
}

/// <summary>谋诸葛亮-空城：事件获得的先古能力。</summary>
[RegisterCard(typeof(TokenCardPool))]
public sealed class MouZhugeliangKongCheng : WuZhugeliangCard
{
    public MouZhugeliangKongCheng() : base(3, CardType.Power, CardRarity.Ancient, TargetType.Self) { }
    public override bool CanBeGeneratedInCombat => false;
    public override string CustomPortraitPath => "mou_zhugeliang_kong_cheng.png".CardImagePath();
    public override IEnumerable<CardTag> Tags => SunqianMechanicBoundTag.With(base.Tags);
    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Shu];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<XingPower>()];

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        PowerCmd.Apply<MouKongChengPower>(context, Owner.Creature, IsUpgraded ? 1 : 2, Owner.Creature, this, false);

    protected override void OnUpgrade() { }
}

[RegisterPower]
public sealed class ZhuangShiPreparationPower : ZhuangShiPreparationPowerBase
{
    protected override int StrengthPerDiscard => 2;
    protected override bool UsesAoVfx => false;
    protected override Task ApplyTemporaryStrength(PlayerChoiceContext context, decimal amount) =>
        PowerCmd.Apply<ZhuangShiTemporaryStrengthPower>(context, Owner, amount, Owner, null, false);
}

[RegisterPower]
public sealed class ZhuangShiAoPreparationPower : ZhuangShiPreparationPowerBase
{
    protected override int StrengthPerDiscard => 4;
    protected override bool UsesAoVfx => true;
    protected override Task ApplyTemporaryStrength(PlayerChoiceContext context, decimal amount) =>
        PowerCmd.Apply<ZhuangShiAoTemporaryStrengthPower>(context, Owner, amount, Owner, null, false);
}

public abstract class ZhuangShiPreparationPowerBase : ModPowerTemplate
{
    protected abstract int StrengthPerDiscard { get; }
    protected abstract bool UsesAoVfx { get; }
    protected abstract Task ApplyTemporaryStrength(PlayerChoiceContext context, decimal amount);
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    protected override bool IsVisibleInternal => false;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        await base.AfterPlayerTurnStart(context, player);
        var hand = PileTypeExtensions.GetPile(PileType.Hand, player)?.Cards;
        if (player.Creature != Owner || Owner.IsDead || hand == null || hand.Count == 0) return;
        var selected = (await CardSelectCmd.FromHand(context, player,
            new CardSelectorPrefs(new("cards", "zhuang_shi.select_discard"), 0, hand.Count), _ => true, this)).ToList();
        if (selected.Count == 0) return;
        // 原版批量弃牌会先弃完选中的牌，再触发狡黠自动打出。
        await CardCmd.Discard(context, selected);
        if (!Owner.IsAlive) return;
        _ = ShiWeiYanVfx.Play(UsesAoVfx ? ShiWeiYanVfxStyle.ZhuangShiAo : ShiWeiYanVfxStyle.ZhuangShi);
        await EventAncientEffects.LoseHp(context, Owner, selected.Count * 10m);
        if (!Owner.IsAlive) return;
        await ApplyTemporaryStrength(context, selected.Count * StrengthPerDiscard);
        await PowerCmd.Apply<ZhuangShiFreePower>(context, Owner, selected.Count, Owner, null, false);
    }
}

/// <summary>壮誓获得的临时力量；必须是可实例化的原生临时力量子类。</summary>
[RegisterPower]
public sealed class ZhuangShiTemporaryStrengthPower : TemporaryStrengthPower, IModPowerAssetOverrides
{
    public override AbstractModel OriginModel => ModelDb.Card<ShiWeiYanZhuangShi>();
    public override LocString Title => new("powers", "ZHUANG_SHI_TEMPORARY_STRENGTH_POWER.title");
    public override LocString Description => new("powers", "ZHUANG_SHI_TEMPORARY_STRENGTH_POWER.description");
    public PowerAssetProfile AssetProfile => new(CustomIconPath, CustomBigIconPath);
    public string CustomIconPath => "res://Yjcm/Images/Powers/kuang_gu_buff.png";
    public string CustomBigIconPath => CustomIconPath;
}

/// <summary>壮誓-傲获得的临时力量。</summary>
[RegisterPower]
public sealed class ZhuangShiAoTemporaryStrengthPower : TemporaryStrengthPower, IModPowerAssetOverrides
{
    public override AbstractModel OriginModel => ModelDb.Card<ShiWeiYanZhuangShiAo>();
    public override LocString Title => new("powers", "ZHUANG_SHI_AO_TEMPORARY_STRENGTH_POWER.title");
    public override LocString Description => new("powers", "ZHUANG_SHI_AO_TEMPORARY_STRENGTH_POWER.description");
    public PowerAssetProfile AssetProfile => new(CustomIconPath, CustomBigIconPath);
    public string CustomIconPath => "res://Yjcm/Images/Powers/kuang_gu_buff.png";
    public string CustomBigIconPath => CustomIconPath;
}

[RegisterPower]
public sealed class ZhuangShiFreePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomIconPath => "res://Yjcm/Images/Powers/kuang_gu_buff.png";
    public override string CustomBigIconPath => CustomIconPath;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner?.Creature != Owner || card.Pile?.Type is not (PileType.Hand or PileType.Play)) return false;
        modifiedCost = 0;
        return true;
    }

    public override Task BeforeCardPlayed(CardPlay play) =>
        play.Card.Owner?.Creature == Owner ? PowerCmd.Decrement(this) : Task.CompletedTask;
}

[RegisterPower]
public sealed class KuangGuPower : KuangGuPowerBase
{
    protected override bool IsAo => false;
    public override string CustomIconPath => "res://Yjcm/Images/Powers/kuang_gu_buff.png";
}

[RegisterPower]
public sealed class KuangGuAoPower : KuangGuPowerBase
{
    protected override bool IsAo => true;
    public override string CustomIconPath => "res://Yjcm/Images/Powers/kuang_gu_ao_buff.png";
}

public abstract class KuangGuPowerBase : ModPowerTemplate
{
    protected abstract bool IsAo { get; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override string CustomBigIconPath => CustomIconPath;

    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? source)
    {
        await base.AfterDamageGiven(context, dealer, result, props, target, source);
        if (dealer != Owner || result.TotalDamage < 10m) return;
        await CreatureCmd.Heal(Owner, 10);
        await CardPileCmd.Draw(context, 1, Owner.Player);
        if (!IsAo || Owner.IsDead || PileTypeExtensions.GetPile(PileType.Hand, Owner.Player)?.Cards.Count is not > 0) return;
        if (!LocalContext.NetId.HasValue) return;

        // 狡黠牌在壮誓的弃牌动作中自动打出时，伤害钩子沿用该动作的选择上下文。
        // 在此新建动作并等待其完成会让新动作排在当前动作之后，形成互等死锁。
        if (context is not ThrowingPlayerChoiceContext)
        {
            await DiscardForEnergy(context);
            return;
        }

        // 个别无选择上下文的伤害来源仍须独立排入玩家动作队列；
        // 只等到入队/暂停，不能在当前伤害动作里等待新动作执行完毕。
#if GAME_BETA
        var selectionContext = new HookPlayerChoiceContext(this, Owner.Player, LocalContext.NetId.Value, GameActionType.Combat);
#else
        var selectionContext = new HookPlayerChoiceContext(this, LocalContext.NetId.Value, Owner.CombatState, GameActionType.Combat);
#endif
        var selectionTask = DiscardForEnergy(selectionContext);
        await selectionContext.AssignTaskAndWaitForPauseOrCompletion(selectionTask);
    }

    private async Task DiscardForEnergy(PlayerChoiceContext context)
    {
        if (Owner.IsDead || PileTypeExtensions.GetPile(PileType.Hand, Owner.Player)?.Cards.Count is not > 0) return;
        var card = (await CardSelectCmd.FromHand(context, Owner.Player,
            new CardSelectorPrefs(new("cards", "kuang_gu_ao.select_discard"), 0, 1), _ => true, this)).FirstOrDefault();
        if (card == null) return;
        await CardCmd.Discard(context, card);
        await PlayerCmd.GainEnergy(1, Owner.Player);
    }
}

[RegisterPower]
public sealed class MouKongChengPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomIconPath => "res://Yjcm/Images/Powers/mou_kong_cheng.png";
    public override string CustomBigIconPath => CustomIconPath;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        await base.AfterPlayerTurnStart(context, player);
        if (player.Creature != Owner) return;
        int stars = (int)(Owner.Powers.OfType<XingPower>().FirstOrDefault()?.Amount ?? 0m);
        if (stars > 0) await CardPileCmd.Draw(context, stars, player);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEnd(context, side, participants);
        if (!participants.Contains(Owner)) return;
        var xing = Owner.Powers.OfType<XingPower>().FirstOrDefault();
        int stars = (int)(xing?.Amount ?? 0m);
        if (stars > 0) await CreatureCmd.GainBlock(Owner, stars * 4m, ValueProp.Move, null);
        int loss = Math.Min(Amount, stars);
        if (loss > 0 && xing != null)
            await PowerCmd.ModifyAmount(context, xing, -loss, Owner, null, false);
    }
}
