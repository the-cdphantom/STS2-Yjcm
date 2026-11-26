using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.CardTargeting;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_HU_JIA")]
public sealed class WeiCaoCaoHuJia : WeiCaoCaoCard
{
    public WeiCaoCaoHuJia() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { }
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override string CustomPortraitPath => "res://Experimental/WeiCaoCao/images/hu_jia.png";
    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Wei];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [WuJiangHoverTip.ForFaction(YjcmCardTags.Wei)];

    internal static bool IsCandidate(CardModel card, CardModel source) => card.Id != source.Id &&
        card.Tags.Contains(YjcmCardTags.WuJiang) && card.Tags.Contains(YjcmCardTags.Wei) &&
        card.Rarity is not (CardRarity.Token or CardRarity.Ancient);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiCaoCaoAudio.Play("hu_jia");
        var candidates = Pool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(card => IsCandidate(card, this)).DistinctBy(card => card.Id).ToList();
        if (candidates.Count == 0) return;
        var options = CardFactory.GetDistinctForCombat(Owner, candidates, Math.Min(3, candidates.Count),
            Owner.RunState.Rng.CombatCardGeneration).ToList();
        if (options.Count == 0) return;
        // Match the native Discovery flow, which closes its selection overlay before returning the card.
        var chosen = options.Count == 1 ? options[0] : await CardSelectCmd.FromChooseACardScreen(context, options, Owner);
        if (chosen == null || !WeiCaoCaoDesignRules.Alive(this)) return;
        chosen.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner, CardPilePosition.Random);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

internal static class WeiCaoCaoTargetTypes
{
    internal static readonly TargetType AnyCharacter = CustomTargetType.RegisterSingleTargetType(
        MainFile.ModId, "CAO_CAO_ANY_CHARACTER",
        static (target, player) => target.IsAlive && !target.IsPet && (target.IsPlayer || target.IsMonster));
    internal static readonly TargetType SelfOrEnemy = CustomTargetType.RegisterSingleTargetType(
        MainFile.ModId, "DUO_YUE_SELF_OR_ENEMY",
        static (target, player) => target.IsAlive && !target.IsPet &&
            (target == player.Creature || target.IsMonster));
}

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_DUO_YUE")]
public sealed class WeiCaoCaoDuoYue() : WeiCaoCaoDuoYueBase(false)
{
    public override string CustomPortraitPath => "res://Experimental/WeiCaoCao/images/duo_yue.png";
}

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_JIE_DUO_YUE")]
public sealed class WeiCaoCaoJieDuoYue() : WeiCaoCaoDuoYueBase(true)
{
    public override string CustomPortraitPath => "res://Experimental/WeiCaoCao/images/jie_duo_yue.png";
    public override bool CanBeGeneratedInCombat => false;
    protected override void PlayVoice(bool self) => WeiCaoCaoAudio.PlayVariant("jie_duo_yue", 2);
}

public abstract class WeiCaoCaoDuoYueBase(bool ancient)
    : WeiCaoCaoCard(1, CardType.Attack, ancient ? CardRarity.Ancient : CardRarity.Basic, WeiCaoCaoTargetTypes.SelfOrEnemy)
{
    private bool _spendingJunHe;
    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Wei];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new WeiCaoCaoDuoYueDamageVar(), new DynamicVar("Factor", 6)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [WuJiangHoverTip.ForFaction(YjcmCardTags.Wei), HoverTipFactory.FromPower<WeiCaoCaoJunHePower>()];

    [SavedProperty] public int PlaysThisTurn { get; set; }
    internal int NextPlayNumber => PlaysThisTurn + 1;
    internal decimal NextBaseDamage => NextPlayNumber * DynamicVars["Factor"].BaseValue;
    internal int DrawCount(int playNumber) => playNumber + (ancient ? 1 : 0);
    internal decimal JunHeMultiplier => ancient ? 3m : 2m;
    protected virtual void PlayVoice(bool self) => WeiCaoCaoAudio.Play(self ? "duo_yue_draw" : "duo_yue_attack");

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("CurrentDraw", DrawCount(NextPlayNumber));
    }

    public override Task BeforeCombatStart()
    {
        PlaysThisTurn = 0;
        _spendingJunHe = false;
        return Task.CompletedTask;
    }
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext context, Player player)
    {
        if (player == Owner) PlaysThisTurn = 0;
        return Task.CompletedTask;
    }

    // Native destination is resolved before OnPlay. Returning through that path
    // avoids putting the card into the hand during discard/draw/replay hooks.
#if GAME_BETA
    protected override CardLocation GetResultLocationForCardPlay()
    {
        var original = base.GetResultLocationForCardPlay();
        return original.pileType == PileType.Discard && CurrentTarget == Owner.Creature && NextPlayNumber < 3
            ? new CardLocation(Owner, PileType.Hand, CardPilePosition.Bottom) : original;
    }
#else
    protected override PileType GetResultPileTypeForCardPlay()
    {
        var original = base.GetResultPileTypeForCardPlay();
        return original == PileType.Discard && CurrentTarget == Owner.Creature && NextPlayNumber < 3
            ? PileType.Hand : original;
    }
#endif

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        int number = ++PlaysThisTurn; // Reserve this play before nested discard effects.
        bool self = play.Target == Owner.Creature;
        PlayVoice(self);
        var hand = PileType.Hand.GetPile(Owner);
        if (hand.Cards.Count > 0)
        {
            var discarded = (await CardSelectCmd.FromHandForDiscard(context, Owner,
                new CardSelectorPrefs(new LocString("cards", "YJCM_CARD_WEI_CAO_CAO_DUO_YUE.select_prompt"), 1, 1)
                { Cancelable = false, RequireManualConfirmation = true }, null, this)).ToArray();
            if (discarded.Length > 0) await CardCmd.Discard(context, discarded);
        }
        if (Owner.Creature.IsDead) return;
        if (self)
        {
            await CardPileCmd.Draw(context, DrawCount(number), Owner, false);
            // A native replay can cross the third-play boundary after the initial
            // destination was selected. End in discard instead of returning again.
            if (number >= 3 && play.PlayCount > 1 && play.PlayIndex == play.PlayCount - 1 &&
                play.ResultPile == PileType.Hand && Pile?.Type == PileType.Play)
                await CardPileCmd.Add(this, PileType.Discard, CardPilePosition.Bottom);
            return;
        }
        if (play.Target is not { IsAlive: true } target) return;
        _spendingJunHe = await WeiCaoCaoJunHePower.TryConsume(context, Owner.Creature, 1, this);
        try
        {
#if GAME_BETA
            await DamageCmd.Attack(number * DynamicVars["Factor"].BaseValue).FromCard(this, play)
#else
            await DamageCmd.Attack(number * DynamicVars["Factor"].BaseValue).FromCard(this)
#endif
                .Targeting(target).Execute(context);
        }
        finally { _spendingJunHe = false; }
    }

#if GAME_BETA
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? play)
#else
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
#endif
        => ReferenceEquals(cardSource, this) && dealer == Owner.Creature && target != Owner.Creature &&
            !props.HasFlag(ValueProp.Unpowered) && (_spendingJunHe || WeiCaoCaoJunHePower.AmountOf(Owner.Creature) > 0)
            ? JunHeMultiplier : 1m;

    protected override void OnUpgrade() => DynamicVars["Factor"].UpgradeValueBy(2);
}

// Damage preview uses the upcoming play count, then the native modifier pipeline
// (including JunHe's final multiplier). No resource is consumed while hovering.
internal sealed class WeiCaoCaoDuoYueDamageVar() : DamageVar(6, ValueProp.Move)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode mode, Creature? target, bool runGlobalHooks)
    {
        if (card is WeiCaoCaoDuoYueBase duoYue) BaseValue = duoYue.NextBaseDamage;
        base.UpdateCardPreview(card, mode, target, runGlobalHooks);
    }
}
