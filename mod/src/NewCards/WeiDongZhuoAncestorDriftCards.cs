using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_ZHU_GE_JING_ZU_YIN")]
public sealed class WeiDongZhuoZhuGeJingZuYin : WeiDongZhuoQunCard
{
    private int _damageChange;
    public WeiDongZhuoZhuGeJingZuYin() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/zu_yin.png";
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(0, ValueProp.Move)];

    [SavedProperty] public bool ReduceNext { get; set; }
    [SavedProperty]
    public int DamageChange
    {
        get => _damageChange;
        set
        {
            AssertMutable();
            int baseline = IsUpgraded ? 10 : 0;
            DynamicVars.Damage.BaseValue = Math.Max(0, baseline + value);
            // Native deserialization restores saved properties before upgrades;
            // preserve negative changes until the ten-point upgrade is restored.
            _damageChange = value;
        }
    }

    internal void ResolveInHandDamage(int damage)
    {
        if (damage <= 0) return;
        DamageChange = (int)Math.Clamp(DynamicVars.Damage.BaseValue + (ReduceNext ? -damage : damage), 0m, 999999999m)
            - (IsUpgraded ? 10 : 0);
        ReduceNext = !ReduceNext;
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("zu_yin.wav");
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue)
            .Targeting(play.Target!).Execute(context);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(10);
        DynamicVars.Damage.BaseValue = Math.Max(0, 10 + _damageChange);
    }
    protected override void AfterDowngraded()
    {
        base.AfterDowngraded();
        DamageChange = _damageChange;
    }
}

internal static class ZuYinDamage
{
    internal readonly record struct Pending(WeiDongZhuoZhuGeJingZuYin Card, int BeforeHalving, bool Reducing);

    internal static Pending[] Prepare(Creature target, ref decimal amount, ValueProp props)
    {
        if (target.Player is not { } player || target.IsDead || amount <= 0 ||
            props.HasFlag(ValueProp.Unpowered)) return [];
        var cards = PileType.Hand.GetPile(player)?.Cards.OfType<WeiDongZhuoZhuGeJingZuYin>().ToArray();
        if (cards is not { Length: > 0 }) return [];
        var pending = new Pending[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            int incoming = (int)Math.Clamp(amount, 0m, 999999999m);
            bool reducing = cards[i].ReduceNext;
            pending[i] = new Pending(cards[i], incoming, reducing);
            if (reducing) amount /= 2m;
        }
        return pending;
    }

    internal static void Commit(Pending[] plan, int hpLost)
    {
        foreach (var pending in plan)
        {
            // Reducing uses this hit's value before this copy halves it. Increasing
            // uses HP actually lost. A 1 -> 0 reduction still advances its phase.
            int damage = pending.Reducing ? pending.BeforeHalving : hpLost;
            pending.Card.ResolveInHandDamage(damage);
        }
    }
}

#if !GAME_V0114
[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
internal static class ZuYinIncomingDamagePatch
{
    private static void Prefix(Creature __instance, ref decimal amount, ValueProp props, out ZuYinDamage.Pending[] __state) =>
        __state = ZuYinDamage.Prepare(__instance, ref amount, props);

    private static void Postfix(DamageResult __result, ZuYinDamage.Pending[] __state) =>
        ZuYinDamage.Commit(__state, __result.UnblockedDamage);
}
#endif

[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_QIN_YI_LU_PIAO_PING")]
public sealed class WeiDongZhuoQinYiLuPiaoPing : WeiDongZhuoQunCard
{
    public WeiDongZhuoQinYiLuPiaoPing() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://WeiDongZhuo/images/piao_ping.png";
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<PiaoPingPower>()];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("piao_ping.wav");
        if (Owner.Creature.Powers.OfType<PiaoPingPower>().Any()) return;
        var power = await PowerCmd.Apply<PiaoPingPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
        power?.SkipActivationPlay(play);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterPower]
public sealed class PiaoPingPower : WeiDongZhuoQunPower
{
    private CardPlay? _activationPlay;
    protected override string LocKey => "PIAO_PING";
    protected override string IconName => "piao_ping";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    [SavedProperty] public bool NextIsDiscard { get; set; }
    [SavedProperty] public int NextCount { get; set; } = 1;
    public override LocString Description
    {
        get
        {
            var description = new LocString("powers", "YJCM_POWER_PIAO_PING.active_description");
            description.Add("DiscardNext", NextIsDiscard);
            description.Add("Count", NextCount);
            return description;
        }
    }

    internal void SkipActivationPlay(CardPlay play) => _activationPlay = play;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player.Creature == Owner) NextCount = 1;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (ReferenceEquals(play, _activationPlay))
        {
            _activationPlay = null;
            return;
        }
        if (play.Card.Owner?.Creature != Owner || !play.IsFirstInSeries || Owner.IsDead || Owner.Player == null) return;

        // Reserve the next phase before draw/discard hooks can auto-play another
        // card. Nested plays must each advance once and retain the same context.
        bool discard = NextIsDiscard;
        int count = Math.Clamp(NextCount, 1, 5);
        NextIsDiscard = !NextIsDiscard;
        NextCount = Math.Min(count + 1, 5);
        if (!discard)
        {
            await CardPileCmd.Draw(context, count, Owner.Player);
            return;
        }
        if (context is not ThrowingPlayerChoiceContext)
        {
            await Discard(context, count);
            return;
        }
        if (!LocalContext.NetId.HasValue) return;
#if GAME_BETA
        var selectionContext = new HookPlayerChoiceContext(this, Owner.Player, LocalContext.NetId.Value, GameActionType.Combat);
#else
        var selectionContext = new HookPlayerChoiceContext(this, LocalContext.NetId.Value, Owner.CombatState, GameActionType.Combat);
#endif
        var task = Discard(selectionContext, count);
        await selectionContext.AssignTaskAndWaitForPauseOrCompletion(task);
    }

    private async Task Discard(PlayerChoiceContext context, int count)
    {
        if (Owner.IsDead || Owner.Player == null) return;
        int required = Math.Min(count, PileType.Hand.GetPile(Owner.Player)?.Cards.Count ?? 0);
        if (required == 0) return;
        var selected = (await CardSelectCmd.FromHandForDiscard(context, Owner.Player,
            new CardSelectorPrefs(new LocString("cards", "YJCM_CARD_PIAO_PING.select_prompt"), required, required),
            _ => true, this)).ToList();
        await CardCmd.Discard(context, selected);
    }
}
