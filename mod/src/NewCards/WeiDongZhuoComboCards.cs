using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using System.Runtime.CompilerServices;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Combat.CardTargeting;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>“连招技”只作为检索标记；具体前置类型由可见悬浮框解释。</summary>
[RegisterOwnedCardTag(nameof(LianZhaoJi))]
[RegisterOwnedCardTag(nameof(LianZhaoJiJiNeng))]
[RegisterOwnedCardTag(nameof(LianZhaoJiGongJi))]
[RegisterOwnedCardTag(nameof(LianZhaoJiShangHai))]
public class WeiDongZhuoComboTags
{
    public static readonly CardTag LianZhaoJi = ModContentRegistry
        .GetQualifiedCardTagId(MainFile.ModId, nameof(LianZhaoJi)).GetModCardTag();
    public static readonly CardTag LianZhaoJiJiNeng = ModContentRegistry
        .GetQualifiedCardTagId(MainFile.ModId, nameof(LianZhaoJiJiNeng)).GetModCardTag();
    public static readonly CardTag LianZhaoJiGongJi = ModContentRegistry
        .GetQualifiedCardTagId(MainFile.ModId, nameof(LianZhaoJiGongJi)).GetModCardTag();
    public static readonly CardTag LianZhaoJiShangHai = ModContentRegistry
        .GetQualifiedCardTagId(MainFile.ModId, nameof(LianZhaoJiShangHai)).GetModCardTag();
}

internal static class WeiDongZhuoTargetTypes
{
    /// <summary>据陲只能选择使用者本人或存活敌人，不允许误选联机队友或宠物。</summary>
    internal static readonly TargetType SelfOrEnemy = CustomTargetType.RegisterSingleTargetType(
        MainFile.ModId,
        "JU_CHUI_SELF_OR_ENEMY",
        static (candidate, user) => candidate.IsAlive && !candidate.IsPet &&
                                    (candidate == user.Creature || candidate.IsMonster));
}

internal static class WeiDongZhuoComboHistory
{
    private sealed class CombatComboCounts
    {
        internal readonly Dictionary<MegaCrit.Sts2.Core.Entities.Players.Player, HashSet<CardPlay>> Plays = new();
    }

    private static readonly ConditionalWeakTable<ICombatState, CombatComboCounts> Counts = new();

    internal static int CountFor(CardModel card)
    {
        var state = card.CombatState ?? card.Owner?.Creature.CombatState;
        if (state == null || card.Owner == null || !Counts.TryGetValue(state, out var counter))
            return 0;
        return counter.Plays.TryGetValue(card.Owner, out var plays) ? plays.Count : 0;
    }

    /// <summary>同一玩家本场战斗成功触发的连招技总数；同一次自动/重复打出只登记一次。</summary>
    internal static int RecordTriggered(CardPlay play)
    {
        var player = WeiDongZhuoRandomTargetScope.PlayerOf(play);
        var state = play.Card.CombatState ?? player.Creature.CombatState;
        if (state == null) return 0;

        var counts = Counts.GetValue(state, _ => new CombatComboCounts()).Plays;
        if (!counts.TryGetValue(player, out var plays))
            counts[player] = plays = new HashSet<CardPlay>();
        if (!plays.Add(play)) return plays.Count;

        int count = plays.Count;
        if (player.PlayerCombatState != null)
        {
            foreach (var pile in player.PlayerCombatState.AllPiles)
                foreach (var jiZhan in pile.Cards.OfType<WeiDongZhuoWeiMaChaoJiZhan>())
                    jiZhan.SetComboCount(count);
        }
        if (play.Card is WeiDongZhuoWeiMaChaoJiZhan current)
            current.SetComboCount(count);
        return count;
    }

    /// <summary>
    /// CardPlayStarted is recorded before OnPlay. Therefore the last matching entry is
    /// the current play and the entry immediately before it is the combo predecessor.
    /// Replay/AutoPlay each create their own entry and deliberately count as plays.
    /// </summary>
    internal static bool ImmediatelyFollows(CardPlay current, CardType requiredType)
        => Previous(current)?.Type == requiredType;

    internal static bool ImmediatelyFollowsDamage(CardPlay current)
    {
        var previous = Previous(current);
        // “伤害牌”目前按会造成伤害的攻击牌结算；保留独立标签，后续可无损扩展到
        // 非攻击类型的伤害牌，而不改变“连招技-攻击牌”的语义。
        return previous?.Type == CardType.Attack;
    }

    internal static CardModel? Previous(CardPlay current)
    {
        var state = current.Card.CombatState;
        if (state == null) return null;
        var entries = CombatManager.Instance.History.CardPlaysStarted
            .Where(e => e.HappenedThisTurn(state) && e.Actor == current.Card.Owner.Creature)
            .ToList();
        if (entries.Count < 2) return null;
        var latest = entries[^1].CardPlay;
        if (!ReferenceEquals(latest.Card, current.Card) || latest.PlayIndex != current.PlayIndex)
            return null;
        return entries[^2].CardPlay.Card;
    }
}

public abstract class WeiDongZhuoJuChuiBase : WeiDongZhuoCard
{
    private const string ComboHealKey = "ComboHeal";
    private const string ComboLossKey = "ComboLoss";
    private readonly bool _ancient;

    protected WeiDongZhuoJuChuiBase(bool ancient)
        : base(1, CardType.Attack, ancient ? CardRarity.Ancient : CardRarity.Basic,
            WeiDongZhuoTargetTypes.SelfOrEnemy)
    {
        _ancient = ancient;
    }

    public override bool GainsBlock => true;

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Qun,
         WeiDongZhuoComboTags.LianZhaoJi, WeiDongZhuoComboTags.LianZhaoJiJiNeng];

    protected override IEnumerable<DynamicVar> CanonicalVars => _ancient
        ? [new DamageVar(6, ValueProp.Move), new BlockVar(12, ValueProp.Move),
           new DynamicVar(ComboHealKey, 9), new DynamicVar(ComboLossKey, 15)]
        : [new DamageVar(6, ValueProp.Move), new BlockVar(6, ValueProp.Move),
           new DynamicVar(ComboHealKey, 4), new DynamicVar(ComboLossKey, 8)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [new HoverTip(
            new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_COMBO_SKILL.title"),
            new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_COMBO_SKILL.description"))];

    protected abstract string NormalVoicePath { get; }
    protected abstract string ComboVoicePath { get; }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool combo = WeiDongZhuoComboHistory.ImmediatelyFollows(cardPlay, CardType.Skill);
        if (combo) WeiDongZhuoComboHistory.RecordTriggered(cardPlay);
        VoicePlayer.PlayWav(combo ? ComboVoicePath : NormalVoicePath);
        if (combo) _ = WeiDongZhuoComboVfx.Play(WeiDongZhuoComboVfxStyle.JuChui);

        var target = cardPlay.Target!;
        if (target == Owner.Creature)
        {
            await CreatureCmd.GainBlock(target, DynamicVars.Block, cardPlay);
            if (combo && Owner.Creature.IsAlive)
                await CreatureCmd.Heal(Owner.Creature, DynamicVars[ComboHealKey].BaseValue);
        }
        else
        {

#if GAME_BETA
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
#else
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)
#endif
                .Targeting(target).Execute(choiceContext);
            if (combo && target.IsAlive)
                await WeiDongZhuoQunEffects.LoseHp(
                    choiceContext, target, DynamicVars[ComboLossKey].BaseValue);
        }
    }

    protected override void OnUpgrade()
    {
        if (!_ancient)
            DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars[ComboHealKey].UpgradeValueBy(3);
        DynamicVars[ComboLossKey].UpgradeValueBy(_ancient ? 5 : 4);
    }

    protected static string Voice(string file) => Path.Combine(
        Path.GetDirectoryName(typeof(WeiDongZhuoJuChuiBase).Assembly.Location)!,
        "Voice", "WeiDongZhuo", file);
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WEI_DONG_ZHUO_JU_CHUI")]
public sealed class WeiDongZhuoJuChui : WeiDongZhuoJuChuiBase
{
    public WeiDongZhuoJuChui() : base(false) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/ju_chui.png";
    protected override string NormalVoicePath => Voice("ju_chui_normal.wav");
    protected override string ComboVoicePath => Voice("ju_chui_combo.wav");
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WEI_DONG_ZHUO_JIE_JU_CHUI")]
public sealed class WeiDongZhuoJieJuChui : WeiDongZhuoJuChuiBase
{
    public WeiDongZhuoJieJuChui() : base(true) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/jie_ju_chui.png";
    protected override string NormalVoicePath => Voice("jie_ju_chui_normal.wav");
    protected override string ComboVoicePath => Voice("jie_ju_chui_combo.wav");
}

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_WEI_DONG_ZHUO_HAO_SHOU")]
public sealed class WeiDongZhuoHaoShou : WeiDongZhuoCard
{
    public WeiDongZhuoHaoShou() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/hao_shou.png";
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override IEnumerable<CardTag> Tags => SunqianMechanicBoundTag.With(base.Tags);
    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Qun];
    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(WeiDongZhuoHaoShou).Assembly.Location)!,
        "Voice", "WeiDongZhuo", "hao_shou.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        var candidates = Owner.Character.CardPool.GetUnlockedCards(
                Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.Id != Id && c.Tags.Contains(YjcmCardTags.WuJiang) && c.Tags.Contains(YjcmCardTags.Qun))
            .ToList();
        if (candidates.Count == 0) return;
        var options = CardFactory.GetDistinctForCombat(Owner, candidates, Math.Min(3, candidates.Count),
            Owner.RunState.Rng.CombatCardGeneration).ToList();
        if (options.Count == 0) return;
        var chosen = await CardSelectCmd.FromSimpleGrid(choiceContext, options, Owner,
            new CardSelectorPrefs(
                new LocString("cards", "YJCM_CARD_WEI_DONG_ZHUO_HAO_SHOU.select_prompt"), 1, 1));
        var card = chosen.FirstOrDefault();
        if (card == null) return;
        card.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
