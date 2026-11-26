using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

internal static class WeiDongZhuoQunEffects
{
    internal const string DefaultPortrait = "res://WeiDongZhuo/images/ju_chui.png";

    internal static string Voice(string file) => Path.Combine(
        Path.GetDirectoryName(typeof(WeiDongZhuoQunEffects).Assembly.Location)!,
        "Voice", "WeiDongZhuo", file);

    internal static void PlayVoice(string? file)
    {
        if (!string.IsNullOrWhiteSpace(file)) VoicePlayer.PlayWav(Voice(file));
    }

    internal static AttackCommand Attack(CardModel card, CardPlay play, decimal damage)
    {
#if GAME_BETA
        return DamageCmd.Attack(damage).FromCard(card, play);
#else
        return DamageCmd.Attack(damage).FromCard(card);
#endif
    }

    /// <summary>
    /// "失去生命"是无来源的特殊伤害：无视格挡且不进入任何力量、易伤、伤害加成等 buff
    /// 的修正管线。不要给它传入卡牌或生物来源，否则会被“造成伤害”类能力误认。
    /// </summary>
    internal static Task LoseHp(PlayerChoiceContext context, Creature creature, decimal amount)
    {
        return CreatureCmd.Damage(context, creature, amount,
            ValueProp.Unblockable | ValueProp.Unpowered, null, null);
    }

    internal static CardSelectorPrefs Prefs(string key, int min, int max) =>
        new(new LocString("cards", key), min, max);
}

public abstract class WeiDongZhuoQunCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool shouldShowInCardLibrary = true)
    : WeiDongZhuoCard(cost, type, rarity, target, shouldShowInCardLibrary)
{
    public override string CustomPortraitPath => WeiDongZhuoQunEffects.DefaultPortrait;
    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Qun];
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_YUAN_YIN_MO_SHOU")]
public sealed class WeiDongZhuoYuanYinMoShou : WeiDongZhuoQunCard
{
    public WeiDongZhuoYuanYinMoShou() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/mo_shou.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Cards", 1)];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("mo_shou.wav");
        await PowerCmd.Apply<MoShouPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
    }
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WEI_DONG_ZHUO_BAO_NUE_XING_TAI")]
public sealed class WeiDongZhuoBaoNueXingTai : WeiDongZhuoCard
{
    public WeiDongZhuoBaoNueXingTai() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/bao_nue_xing_tai.png";
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<BaoNueXingTaiPower>()];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("bao_nue_xing_tai.wav");
        await PowerCmd.Apply<BaoNueXingTaiPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WEI_LV_BU_BA_GUAN")]
public sealed class WeiDongZhuoWeiLvBuBaGuan : WeiDongZhuoQunCard
{
    private const string ComboDamageKey = "ComboDamage";
    public WeiDongZhuoWeiLvBuBaGuan() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/ba_guan.png";
    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Qun, WeiDongZhuoComboTags.LianZhaoJi, WeiDongZhuoComboTags.LianZhaoJiGongJi];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6, ValueProp.Move), new DamageVar(ComboDamageKey, 5, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [ComboTip("ATTACK")];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool combo = WeiDongZhuoComboHistory.ImmediatelyFollows(play, CardType.Attack);
        if (combo) WeiDongZhuoComboHistory.RecordTriggered(play);
        WeiDongZhuoQunEffects.PlayVoice(combo ? "ba_guan_combo.wav" : "ba_guan.wav");
        if (combo) _ = WeiDongZhuoComboVfx.Play(WeiDongZhuoComboVfxStyle.BaGuan);
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .Targeting(play.Target!).Execute(context);
        if (!combo || !play.Target!.IsAlive) return;
        var hand = PileType.Hand.GetPile(Owner);
        int max = hand.Cards.Count;
        if (max <= 0) return;
        var selected = (await CardSelectCmd.FromHandForDiscard(context, Owner,
            WeiDongZhuoQunEffects.Prefs("YJCM_CARD_BA_GUAN.select_prompt", 0, max),
            _ => true, this)).ToList();
        if (selected.Count == 0) return;
        await CardCmd.Discard(context, selected);
        decimal comboDamage = DynamicVars[ComboDamageKey].BaseValue * selected.Count;
        await WeiDongZhuoQunEffects.Attack(this, play, comboDamage)
            .Targeting(play.Target).Execute(context);
    }

    protected override void OnUpgrade() => DynamicVars[ComboDamageKey].UpgradeValueBy(3);
    private static IHoverTip ComboTip(string suffix) => new HoverTip(
        new LocString("cards", $"YJCM_CARD_WEI_DONG_ZHUO_COMBO_{suffix}.title"),
        new LocString("cards", $"YJCM_CARD_WEI_DONG_ZHUO_COMBO_{suffix}.description"));
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WEI_MA_CHAO_JI_ZHAN")]
public sealed class WeiDongZhuoWeiMaChaoJiZhan : WeiDongZhuoQunCard
{
    private const string ComboDamageKey = "ComboDamage";
    private const string RepeatCountKey = "RepeatCount";
    private const string StrengthLossKey = "StrengthLoss";
    public WeiDongZhuoWeiMaChaoJiZhan() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/ji_zhan.png";
    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Qun, WeiDongZhuoComboTags.LianZhaoJi, WeiDongZhuoComboTags.LianZhaoJiJiNeng];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6, ValueProp.Move), new DamageVar(ComboDamageKey, 6, ValueProp.Move),
         new DynamicVar(StrengthLossKey, 1), new DynamicVar(RepeatCountKey, 0)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [new HoverTip(
        new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_COMBO_SKILL.title"),
        new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_COMBO_SKILL.description"))];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool combo = WeiDongZhuoComboHistory.ImmediatelyFollows(play, CardType.Skill);
        int repeats = combo ? WeiDongZhuoComboHistory.RecordTriggered(play) : 0;
        WeiDongZhuoQunEffects.PlayVoice(combo ? "ji_zhan_combo.wav" : "ji_zhan.wav");
        if (combo) _ = WeiDongZhuoComboVfx.Play(WeiDongZhuoComboVfxStyle.JiZhan);
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .Targeting(play.Target!).Execute(context);
        if (!combo) return;
        for (int i = 0; i < repeats && play.Target!.IsAlive; i++)
        {
            await PowerCmd.Apply<StrengthPower>(context, play.Target, -DynamicVars[StrengthLossKey].BaseValue,
                Owner.Creature, this, false);
            await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars[ComboDamageKey].BaseValue)
                .Targeting(play.Target).Execute(context);
        }
    }

    internal void SetComboCount(int count) => DynamicVars[RepeatCountKey].BaseValue = count;

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        await base.AfterCardChangedPiles(card, oldPileType, clonedBy);
        if (ReferenceEquals(card, this))
            SetComboCount(WeiDongZhuoComboHistory.CountFor(this));
    }

    protected override void OnUpgrade()
    {
        DynamicVars[ComboDamageKey].UpgradeValueBy(3);
        DynamicVars[StrengthLossKey].UpgradeValueBy(1);
    }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WEI_ZHANG_LIAO_PO_RONG")]
public sealed class WeiDongZhuoWeiZhangLiaoPoRong : WeiDongZhuoQunCard
{
    private static readonly AsyncLocal<int> ReplayDepth = new();
    public WeiDongZhuoWeiZhangLiaoPoRong() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/po_rong.png";
    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Qun, WeiDongZhuoComboTags.LianZhaoJi, WeiDongZhuoComboTags.LianZhaoJiGongJi];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [new HoverTip(
        new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_COMBO_ATTACK.title"),
        new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_COMBO_ATTACK.description"))];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var previous = ReplayDepth.Value == 0 ? WeiDongZhuoComboHistory.Previous(play) : null;
        bool combo = previous?.Type == CardType.Attack;
        if (combo) WeiDongZhuoComboHistory.RecordTriggered(play);
        WeiDongZhuoQunEffects.PlayVoice(combo ? "po_rong_combo.wav" : "po_rong.wav");
        if (combo) _ = WeiDongZhuoComboVfx.Play(WeiDongZhuoComboVfxStyle.PoRong);
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .Targeting(play.Target!).Execute(context);
        if (!combo || previous == null || !play.Target!.IsAlive) return;
        PoRongDoubleDamagePower? multiplier = null;
        if (IsUpgraded)
            multiplier = await PowerCmd.Apply<PoRongDoubleDamagePower>(context, Owner.Creature, 1, Owner.Creature, this, false);
        ReplayDepth.Value++;
        try
        {
            await CardCmd.AutoPlay(context, previous, play.Target, AutoPlayType.Default, false, false);
        }
        finally
        {
            ReplayDepth.Value--;
            if (multiplier != null && multiplier.Owner != null) await PowerCmd.Remove(multiplier);
        }
    }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_XING_DONG_ZHUO_WEI_LIN")]
public sealed class WeiDongZhuoXingDongZhuoWeiLin : WeiDongZhuoQunCard
{
    public WeiDongZhuoXingDongZhuoWeiLin() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/wei_lin.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("wei_lin.wav");
        bool stronger = play.Target!.CurrentHp < Owner.Creature.CurrentHp;
        if (stronger) await YjcmCreatureCompat.LoseMaxHp(context, Owner.Creature, 1, this);
        var attack = await WeiDongZhuoQunEffects.Attack(this, play,
            DynamicVars.Damage.BaseValue * (stronger ? 2 : 1)).Targeting(play.Target).Execute(context);
        if (attack.Results.SelectMany(x => x).Any(r => r.WasTargetKilled))
            await CreatureCmd.GainMaxHp(Owner.Creature, 2);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_XING_YUAN_SHAO_ZONG_SHI")]
public sealed class WeiDongZhuoXingYuanShaoZongShi : WeiDongZhuoQunCard
{
    public WeiDongZhuoXingYuanShaoZongShi() : base(3, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/zong_shi.png";

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("zong_shi.wav");
        var hand = PileType.Hand.GetPile(Owner);
        if (hand.Cards.Count == 0) return;
        var chosen = (await CardSelectCmd.FromHand(context, Owner,
            WeiDongZhuoQunEffects.Prefs("YJCM_CARD_ZONG_SHI.select_prompt", 1, 1),
            _ => true, this)).FirstOrDefault();
        if (chosen == null) return;
        var others = hand.Cards.Where(c => c != chosen && c.Type == chosen.Type).ToList();
        foreach (var card in others) await CardCmd.Exhaust(context, card, false, false);
        var copies = new List<CardModel>();
        for (int i = 0; i < others.Count; i++)
        {
            var copy = CombatState.CloneCard(chosen);
            if (IsUpgraded && copy.IsUpgradable) copy.UpgradeInternal();
            copies.Add(copy);
        }
        if (copies.Count > 0)
            await CardPileCmd.Add(copies, PileType.Hand, CardPilePosition.Random, this, false);
    }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WEI_GONG_SUN_ZAN_ZHUI_TAO")]
public sealed class WeiDongZhuoWeiGongSunZanZhuiTao : WeiDongZhuoQunCard
{
    public WeiDongZhuoWeiGongSunZanZhuiTao() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/zhui_tao.png";
    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Qun, WeiDongZhuoComboTags.LianZhaoJi, WeiDongZhuoComboTags.LianZhaoJiGongJi];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [new HoverTip(new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_COMBO_ATTACK.title"),
             new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_COMBO_ATTACK.description")),
         HoverTipFactory.FromPower<ZhuiTaoPower>()];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool combo = WeiDongZhuoComboHistory.ImmediatelyFollows(play, CardType.Attack);
        if (combo) WeiDongZhuoComboHistory.RecordTriggered(play);
        WeiDongZhuoQunEffects.PlayVoice(combo ? "zhui_tao_combo.wav" : "zhui_tao.wav");
        if (combo) _ = WeiDongZhuoComboVfx.Play(WeiDongZhuoComboVfxStyle.ZhuiTao);
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .Targeting(play.Target!).Execute(context);
        if (!combo || !play.Target!.IsAlive) return;
        await CardPileCmd.Draw(context, 2, Owner);
        await PowerCmd.Apply<ZhuiTaoPower>(context, play.Target, IsUpgraded ? 2 : 1,
            Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WEI_MA_TENG_XIAO_BEN")]
public sealed class WeiDongZhuoWeiMaTengXiaoBen : WeiDongZhuoQunCard
{
    public WeiDongZhuoWeiMaTengXiaoBen() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/xiao_ben.png";
    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Qun, WeiDongZhuoComboTags.LianZhaoJi, WeiDongZhuoComboTags.LianZhaoJiGongJi];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [new HoverTip(new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_COMBO_ATTACK.title"),
             new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_COMBO_ATTACK.description")),
         HoverTipFactory.FromPower<QiPower>()];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool combo = WeiDongZhuoComboHistory.ImmediatelyFollows(play, CardType.Attack);
        if (combo) WeiDongZhuoComboHistory.RecordTriggered(play);
        WeiDongZhuoQunEffects.PlayVoice(combo ? "xiao_ben_combo.wav" : "xiao_ben.wav");
        if (combo) _ = WeiDongZhuoComboVfx.Play(WeiDongZhuoComboVfxStyle.XiaoBen);
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .Targeting(play.Target!).Execute(context);
        if (!combo) return;

        var targets = IsUpgraded
            ? CombatState.HittableEnemies.Where(enemy => enemy.IsAlive).ToList()
            : [play.Target!];
        foreach (var target in targets)
            await PowerCmd.Apply<QiPower>(context, target, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WEI_ZHANG_LIAO_YU_XI")]
public sealed class WeiDongZhuoWeiZhangLiaoYuXi : WeiDongZhuoQunCard
{
    public WeiDongZhuoWeiZhangLiaoYuXi() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/yu_xi.png";
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("yu_xi.wav");
        if (IsUpgraded)
            await PowerCmd.Apply<YuXiPlusPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
        else
            await PowerCmd.Apply<YuXiPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
    }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_NIU_FU_XIAO_XI")]
public sealed class WeiDongZhuoNiuFuXiaoXi : WeiDongZhuoQunCard
{
    public WeiDongZhuoNiuFuXiaoXi() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/xiao_xi.png";
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(10, ValueProp.Move), new DynamicVar("Hits", 2)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("xiao_xi.wav");
        await YjcmCreatureCompat.LoseMaxHp(context, Owner.Creature, 2, this);
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars["Hits"].IntValue).Targeting(play.Target!).Execute(context);
    }
    protected override void OnUpgrade() => DynamicVars["Hits"].UpgradeValueBy(1);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WU_PU_DUAN_TI")]
public sealed class WeiDongZhuoWuPuDuanTi : WeiDongZhuoQunCard
{
    public WeiDongZhuoWuPuDuanTi() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/duan_ti.png";
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("duan_ti.wav");
        await PowerCmd.Apply<DuanTiPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
    }
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_XU_RONG_XIONG_HUO")]
public sealed class WeiDongZhuoXuRongXiongHuo : WeiDongZhuoQunCard
{
    public WeiDongZhuoXuRongXiongHuo() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/xiong_huo.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<XiongHuoPower>()];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("xiong_huo.wav");
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .Targeting(play.Target!).Execute(context);
        if (play.Target.IsAlive)
            await PowerCmd.Apply<XiongHuoPower>(context, play.Target, 1, Owner.Creature, this, false);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_XU_RONG_SHA_JUE")]
public sealed class WeiDongZhuoXuRongShaJue : WeiDongZhuoQunCard
{
    public WeiDongZhuoXuRongShaJue() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/sha_jue.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("sha_jue.wav");
        var attack = await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .Targeting(play.Target!).Execute(context);
        int gain = attack.Results.SelectMany(x => x).Where(r => r.WasTargetKilled)
            .Select(r => Math.Min(5, r.OverkillDamage)).DefaultIfEmpty(0).Max();
        if (gain > 0) await CreatureCmd.GainMaxHp(Owner.Creature, gain);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_XING_YUAN_SHAO_XIAO_YAN")]
public sealed class WeiDongZhuoXingYuanShaoXiaoYan : WeiDongZhuoQunCard
{
    public WeiDongZhuoXingYuanShaoXiaoYan() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/xiao_yan.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("xiao_yan.wav");
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .TargetingAllOpponents(CombatState).Execute(context);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_XING_YUAN_SHAO_JIAO_WANG")]
public sealed class WeiDongZhuoXingYuanShaoJiaoWang : WeiDongZhuoQunCard
{
    public WeiDongZhuoXingYuanShaoJiaoWang() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/xiao_yan.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(15, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("jiao_wang.wav");
        await YjcmCreatureCompat.LoseMaxHp(context, Owner.Creature, 2, this);
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .TargetingAllOpponents(CombatState).Execute(context);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(6);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_MOU_ZHANG_XIU_FU_XI")]
public sealed class WeiDongZhuoMouZhangXiuFuXi : WeiDongZhuoQunCard
{
    public WeiDongZhuoMouZhangXiuFuXi() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/fu_xi.png";
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(8, ValueProp.Move), new DynamicVar("Weak", 1), new DynamicVar("Strength", 2)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("fu_xi.wav");
        if (await ResolveTargetIntent(context, play.Target!))
        {
            await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
                .Targeting(play.Target).Execute(context);
            if (play.Target.IsAlive)
                await PowerCmd.Apply<WeakPower>(context, play.Target, DynamicVars["Weak"].BaseValue,
                    Owner.Creature, this, false);
        }
        else
        {
            await PowerCmd.Apply<StrengthPower>(context, Owner.Creature, DynamicVars["Strength"].BaseValue,
                Owner.Creature, this, false);
            if (play.Target.IsAlive)
                await PowerCmd.Apply<StrengthPower>(context, play.Target, 1, Owner.Creature, this, false);
        }
    }

    private async Task<bool> ResolveTargetIntent(PlayerChoiceContext context, Creature target)
    {
        bool intendsToAttack = target.Monster?.IntendsToAttack == true;
        if (TestMode.IsOn || RunManager.Instance.NetService.Type == NetGameType.Replay)
            return intendsToAttack;

        uint choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(Owner);
#if GAME_BETA
        await context.SignalPlayerChoiceBegun(Owner, PlayerChoiceOptions.CancelPlayCardActions);
#else
        await context.SignalPlayerChoiceBegun(PlayerChoiceOptions.CancelPlayCardActions);
#endif
        try
        {
            if (LocalContext.IsMe(Owner))
            {
                RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
                    Owner, choiceId, PlayerChoiceResult.FromIndex(intendsToAttack ? 1 : 0));
                return intendsToAttack;
            }

            return (await RunManager.Instance.PlayerChoiceSynchronizer
                .WaitForRemoteChoice(Owner, choiceId)).AsIndex() == 1;
        }
        finally
        {
            await context.SignalPlayerChoiceEnded();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Weak"].UpgradeValueBy(1);
        DynamicVars["Strength"].UpgradeValueBy(1);
    }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WANG_YUE_JIAN_DAO")]
public sealed class WeiDongZhuoWangYueJianDao : WeiDongZhuoQunCard
{
    public WeiDongZhuoWangYueJianDao() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/wang_yue.png";
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<WeiDongZhuoWuSeSha>(false)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("jian_dao.wav");
        await PowerCmd.Apply<JianDaoPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WANG_YUE_SHI_FENG")]
public sealed class WeiDongZhuoWangYueShiFeng : WeiDongZhuoQunCard
{
    public WeiDongZhuoWangYueShiFeng() : base(1, CardType.Power, CardRarity.Common, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/wang_yue.png";
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<WeiDongZhuoJueDou>(IsUpgraded)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("shi_feng.wav");
        if (IsUpgraded)
            await PowerCmd.Apply<ShiFengPlusPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
        else
            await PowerCmd.Apply<ShiFengPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
    }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_MOU_XU_YOU_MO_YOU")]
public sealed class WeiDongZhuoMouXuYouMoYou : WeiDongZhuoQunCard
{
    public WeiDongZhuoMouXuYouMoYou() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/mo_you.png";
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("mo_you.wav");
        await PowerCmd.Apply<MoYouPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_MOU_JU_SHOU_ZUO_JUN")]
public sealed class WeiDongZhuoMouJuShouZuoJun : WeiDongZhuoQunCard
{
    public WeiDongZhuoMouJuShouZuoJun() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/zuo_jun.png";
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("zuo_jun.wav");
        // Draw() 在抽牌堆耗尽、洗入弃牌堆继续抽时，返回列表可能不包含后半段实际进入手牌的牌。
        // 用前后手牌差集才能保证所有这次真正摸到的牌都接受同一种选择。
        var hand = PileType.Hand.GetPile(Owner);
        var existingHandCards = hand.Cards.ToHashSet();
        await CardPileCmd.Draw(context, 3, Owner, false);
        var drawn = hand.Cards.Where(card => !existingHandCards.Contains(card)).ToList();
        if (drawn.Count == 0) return;
        var options = new CardModel[]
        {
            CombatState.CreateCard<WeiDongZhuoZuoJunRetainOption>(Owner),
            CombatState.CreateCard<WeiDongZhuoZuoJunFreeOption>(Owner)
        };
        var option = await CardSelectCmd.FromChooseACardScreen(context, options, Owner, false);
        foreach (var card in drawn)
        {
            if (option is WeiDongZhuoZuoJunRetainOption) card.GiveSingleTurnRetain();
            else
            {
                card.SetToFreeThisTurn();
                card.ExhaustOnNextPlay = true;
            }
        }
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_DING_YUAN_XIAN_SHUAI")]
public sealed class WeiDongZhuoDingYuanXianShuai : WeiDongZhuoQunCard
{
    public WeiDongZhuoDingYuanXianShuai() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/xian_shuai.png";
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(4, ValueProp.Move), new DamageVar("ExtraDamage", 2, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("xian_shuai.wav");
        bool first = CombatManager.Instance.History.CardPlaysStarted
            .Count(e => e.HappenedThisTurn(CombatState) && e.Actor == Owner.Creature) == 1;
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .Targeting(play.Target!).Execute(context);
        if (!first) return;
        await CardPileCmd.Draw(context, 1, Owner);
        if (play.Target.IsAlive)
            await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars["ExtraDamage"].BaseValue)
                .Targeting(play.Target).Execute(context);
    }
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["ExtraDamage"].UpgradeValueBy(1);
    }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_MA_TIE_QU_XIAN")]
public sealed class WeiDongZhuoMaTieQuXian : WeiDongZhuoQunCard
{
    public WeiDongZhuoMaTieQuXian() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/qu_xian.png";
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<WeiDongZhuoWuSeSha>(false)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("qu_xian.wav");
        await PowerCmd.Apply<QuXianPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_ZHANG_HUAN_SHU_GUO")]
public sealed class WeiDongZhuoZhangHuanShuGuo : WeiDongZhuoQunCard
{
    public WeiDongZhuoZhangHuanShuGuo() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/shu_guo.png";
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("shu_guo.wav");
        var discard = PileType.Discard.GetPile(Owner);
        int count = Math.Min(1, discard.Cards.Count);
        if (count <= 0) return;
        var selected = (await CardSelectCmd.FromCombatPile(context, discard, Owner,
            WeiDongZhuoQunEffects.Prefs("YJCM_CARD_SHU_GUO.select_prompt", count, count))).ToList();
        await CardPileCmd.Add(selected, PileType.Hand, CardPilePosition.Random, this, false);
        // 移动牌会重置某些临时费用修正，必须在它已回到手牌后再给予免费。
        foreach (var card in selected.Where(card => card.Pile?.Type == PileType.Hand))
            card.SetToFreeThisTurn();
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_LI_JUE_GUO_SI_XIONG_SUAN")]
public sealed class WeiDongZhuoLiJueGuoSiXiongSuan : WeiDongZhuoQunCard
{
    public WeiDongZhuoLiJueGuoSiXiongSuan() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/xiong_suan.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("xiong_suan.wav");
        var hand = PileType.Hand.GetPile(Owner);
        if (hand.Cards.Count > 0)
        {
            var selected = (await CardSelectCmd.FromHandForDiscard(context, Owner,
                WeiDongZhuoQunEffects.Prefs("YJCM_CARD_XIONG_SUAN.select_prompt", 1, 1),
                _ => true, this)).FirstOrDefault();
            if (selected != null) await CardCmd.Discard(context, selected);
        }
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .Targeting(play.Target!).Execute(context);
        await CardPileCmd.Draw(context, 3, Owner);
        await WeiDongZhuoQunEffects.LoseHp(context, Owner.Creature, 3);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_YUAN_TAN_YUAN_SHANG_YUAN_XI_NEI_FA")]
public sealed class WeiDongZhuoYuanTanYuanShangYuanXiNeiFa : WeiDongZhuoQunCard
{
    public WeiDongZhuoYuanTanYuanShangYuanXiNeiFa() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/nei_fa.png";
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("nei_fa.wav");
        await CardPileCmd.Draw(context, 3, Owner);
        var hand = PileType.Hand.GetPile(Owner);
        if (hand.Cards.Count == 0) return;
        var discarded = (await CardSelectCmd.FromHandForDiscard(context, Owner,
            WeiDongZhuoQunEffects.Prefs("YJCM_CARD_NEI_FA.select_prompt", 1, 1),
            _ => true, this)).FirstOrDefault();
        if (discarded == null) return;
        await CardCmd.Discard(context, discarded);
        if (discarded.Type == CardType.Attack)
        {
            int freePlays = NeiFaPower.CountBlockedCards(hand.Cards, CardType.Skill);
            var p = await PowerCmd.Apply<NeiFaPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
            if (p != null) p.Configure(CardType.Skill, CardType.Attack, freePlays);
        }
        else if (discarded.Type == CardType.Skill)
        {
            int freePlays = NeiFaPower.CountBlockedCards(hand.Cards, CardType.Attack);
            var p = await PowerCmd.Apply<NeiFaPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
            if (p != null) p.Configure(CardType.Attack, CardType.Skill, freePlays);
        }
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_MOU_DONG_CHENG_DOU_WEI")]
public sealed class WeiDongZhuoMouDongChengDouWei : WeiDongZhuoQunCard
{
    public WeiDongZhuoMouDongChengDouWei() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/dou_wei.png";
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("dou_wei.wav");
        var hand = PileType.Hand.GetPile(Owner);
        var attacks = hand.Cards.Where(c => c.Type == CardType.Attack).ToList();
        if (attacks.Count == 0) return;
        var selected = (await CardSelectCmd.FromHand(context, Owner,
            WeiDongZhuoQunEffects.Prefs("YJCM_CARD_DOU_WEI.select_prompt", 1, 1),
            c => c.Type == CardType.Attack, this)).FirstOrDefault();
        if (selected == null) return;
        var target = play.Target!;
        await CardCmd.AutoPlay(context, selected, target, AutoPlayType.Default, false, false);
        if (!target.IsAlive) await CreatureCmd.GainMaxHp(Owner.Creature, 3);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public abstract class WeiDongZhuoFuMouBase(bool yang)
    : WeiDongZhuoQunCard(3, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override string CustomPortraitPath => yang
        ? "res://WeiDongZhuo/images/fu_mou_yang.png"
        : "res://WeiDongZhuo/images/fu_mou_yin.png";

    public override HashSet<CardKeyword> CanonicalKeywords => [YjcmKeywords.ZhuanHuanJi];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return HoverTipFactory.FromKeyword(YjcmKeywords.ZhuanHuanJi);
            if (yang)
                yield return HoverTipFactory.FromCard<WeiDongZhuoMouJiaXuFuMouYin>(IsUpgraded);
            else
                yield return HoverTipFactory.FromCard<WeiDongZhuoMouJiaXuFuMouYang>(IsUpgraded);
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice(yang ? "fu_mou_yang.wav" : "fu_mou_yin.wav");
        var hand = PileType.Hand.GetPile(Owner);
        int half = IsUpgraded ? (hand.Cards.Count + 1) / 2 : hand.Cards.Count / 2;
        if (half > 0)
        {
            var selected = (await CardSelectCmd.FromHand(context, Owner,
                WeiDongZhuoQunEffects.Prefs(yang
                    ? "YJCM_CARD_FU_MOU_YANG.select_prompt"
                    : "YJCM_CARD_FU_MOU_YIN.select_prompt", 0, half),
                _ => true, this)).ToList();
            if (yang)
            {
                foreach (var card in selected) await CardCmd.Exhaust(context, card, false, false);
                if (selected.Count > 0)
                {
                    await CardPileCmd.Draw(context, selected.Count, Owner);
                    await PlayerCmd.GainEnergy(selected.Count, Owner);
                }
            }
            else
            {
                foreach (var card in selected) card.SetToFreeThisTurn();
            }
        }

        CardModel opposite = yang
            ? CombatState.CreateCard<WeiDongZhuoMouJiaXuFuMouYin>(Owner)
            : CombatState.CreateCard<WeiDongZhuoMouJiaXuFuMouYang>(Owner);
        if (IsUpgraded) opposite.UpgradeInternal();
        await CardPileCmd.AddGeneratedCardToCombat(opposite, PileType.Draw, Owner, CardPilePosition.Random);
    }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_MOU_JIA_XU_FU_MOU_YANG")]
public sealed class WeiDongZhuoMouJiaXuFuMouYang : WeiDongZhuoFuMouBase
{
    public WeiDongZhuoMouJiaXuFuMouYang() : base(true) { }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_MOU_JIA_XU_FU_MOU_YIN")]
public sealed class WeiDongZhuoMouJiaXuFuMouYin : WeiDongZhuoFuMouBase
{
    public WeiDongZhuoMouJiaXuFuMouYin() : base(false) { }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_MOU_JIA_XU_SU_SHEN")]
public sealed class WeiDongZhuoMouJiaXuSuShen : WeiDongZhuoQunCard
{
    public WeiDongZhuoMouJiaXuSuShen() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/su_shen.png";
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<WeiDongZhuoMouJiaXuRuShi>(false)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("su_shen.wav");
        var card = CombatState.CreateCard<WeiDongZhuoMouJiaXuRuShi>(Owner);
        card.RecordedHp = Owner.Creature.CurrentHp;
        card.RecordedHand = PileType.Hand.GetPile(Owner).Cards.Count;
        card.RecordedEnergy = Owner.PlayerCombatState?.Energy ?? 0;
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_MOU_JIA_XU_RU_SHI")]
public sealed class WeiDongZhuoMouJiaXuRuShi : WeiDongZhuoQunCard
{
    public decimal RecordedHp { get; set; }
    public int RecordedHand { get; set; }
    public decimal RecordedEnergy { get; set; }
    public WeiDongZhuoMouJiaXuRuShi()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/ru_shi.png";
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("ru_shi.wav");
        await CreatureCmd.SetCurrentHp(Owner.Creature, Math.Min(RecordedHp, Owner.Creature.MaxHp));
        var hand = PileType.Hand.GetPile(Owner);
        if (hand.Cards.Count < RecordedHand)
            await CardPileCmd.Draw(context, RecordedHand - hand.Cards.Count, Owner);
        else if (hand.Cards.Count > RecordedHand)
        {
            int excess = hand.Cards.Count - RecordedHand;
            var selected = (await CardSelectCmd.FromHandForDiscard(context, Owner,
                WeiDongZhuoQunEffects.Prefs("YJCM_CARD_RU_SHI.select_prompt", excess, excess),
                _ => true, this)).ToList();
            await CardCmd.Discard(context, selected);
        }
        await PlayerCmd.SetEnergy(RecordedEnergy, Owner);
    }
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WU_HUANG_FU_SONG_LIAN_JIE")]
public sealed class WeiDongZhuoWuHuangFuSongLianJie : WeiDongZhuoQunCard
{
    public WeiDongZhuoWuHuangFuSongLianJie() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/lian_jie.png";
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<LianJiePower>()];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("lian_jie.wav");
        await PowerCmd.Apply<LianJiePower>(context, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WU_HUANG_FU_SONG_JIANG_XIAN")]
public sealed class WeiDongZhuoWuHuangFuSongJiangXian : WeiDongZhuoQunCard
{
    private const string StrengthKey = "Strength";
    public WeiDongZhuoWuHuangFuSongJiangXian() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/jiang_xian.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(StrengthKey, 1)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<JiangXianPower>()];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("jiang_xian.wav");
        await PowerCmd.Apply<JiangXianPower>(context, Owner.Creature,
            DynamicVars[StrengthKey].BaseValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => DynamicVars[StrengthKey].UpgradeValueBy(1);
}

// “佐军”的界面专用选项；Token 稀有度且不可生成，不进入奖励、商店或牌库检索。
[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_ZUO_JUN_RETAIN_OPTION")]
public sealed class WeiDongZhuoZuoJunRetainOption : WeiDongZhuoCard
{
    public WeiDongZhuoZuoJunRetainOption()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None, shouldShowInCardLibrary: false) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/zuo_jun.png";
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_ZUO_JUN_FREE_OPTION")]
public sealed class WeiDongZhuoZuoJunFreeOption : WeiDongZhuoCard
{
    public WeiDongZhuoZuoJunFreeOption()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None, shouldShowInCardLibrary: false) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/zuo_jun.png";
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}
