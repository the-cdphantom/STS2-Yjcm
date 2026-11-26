using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

#nullable enable
namespace Yjcm;

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_MOU_DENG_AI_ZHOU_XI")]
public sealed class WeiCaoCaoMouDengAiZhouXi() : WeiCaoCaoDesignCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var enemies = CombatState.HittableEnemies.ToArray();
        var cards = PileType.Hand.GetPile(Owner).Cards.Where(card => card != this &&
            (!WeiCaoCaoFuPlay.NeedsTarget(card.TargetType) || !enemies.Any(card.IsValidTarget))).ToArray();
        int discarded = 0;
        foreach (var card in cards)
        {
            if (!WeiCaoCaoDesignRules.Alive(this)) return;
            if (card.Pile?.Type != PileType.Hand) continue;
            await CardCmd.Discard(context, card);
            discarded++;
        }
        if (discarded == 0 || !WeiCaoCaoDesignRules.Alive(this)) return;

        List<CardModel> options =
        [
            CombatState.CreateCard<WeiCaoCaoChoiceZhouXiDexterity>(Owner),
            CombatState.CreateCard<WeiCaoCaoChoiceZhouXiSteal>(Owner),
            CombatState.CreateCard<WeiCaoCaoChoiceZhouXiDamage>(Owner)
        ];
        List<CardModel> ordered = [];
        while (ordered.Count < discarded && options.Count > 0)
        {
            // Single picks preserve order across clients and allow confirming early.
            var choice = (await WeiCaoCaoDesignRules.Choose(context, this, options, 0, 1, "zhou_xi")).FirstOrDefault();
            if (choice == null) break;
            ordered.Add(choice);
            options.Remove(choice);
        }

        for (int i = 0; i < ordered.Count && WeiCaoCaoDesignRules.Alive(this); i++)
        {
            var choice = ordered[i];
            int repeat = i + 1;
            Creature? damageTarget = null;
            if (choice is WeiCaoCaoChoiceZhouXiDamage)
            {
                var (confirmed, target) = await WeiCaoCaoFuPlay.SelectTarget(
                    context, choice, CombatState.HittableEnemies.FirstOrDefault());
                if (!confirmed) continue;
                damageTarget = target;
            }
            for (int j = 0; j < repeat && WeiCaoCaoDesignRules.Alive(this); j++)
            {
                switch (choice)
                {
                    case WeiCaoCaoChoiceZhouXiDexterity:
                        await PowerCmd.Apply<DexterityPower>(context, Owner.Creature, 1, Owner.Creature, this);
                        break;
                    case WeiCaoCaoChoiceZhouXiSteal:
                        var powers = CombatState.HittableEnemies.SelectMany(enemy => enemy.Powers)
                            .Where(power => WeiDongZhuoGuangYongBase.CanStealOne(power, Owner.Creature)).ToArray();
                        if (powers.Length > 0)
                            await WeiDongZhuoGuangYongBase.StealOne(context, Owner.RunState.Rng.CombatTargets.NextItem(powers)!, this);
                        break;
                    case WeiCaoCaoChoiceZhouXiDamage when damageTarget is { IsAlive: true }:
#if GAME_BETA
                        await CreatureCmd.Damage(context, damageTarget, 6, ValueProp.Unpowered, this, play);
#else
                        await CreatureCmd.Damage(context, damageTarget, 6, ValueProp.Unpowered, Owner.Creature, this);
#endif
                        break;
                }
            }
        }
    }
}

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_CHOICE_ZHOU_XI_DEXTERITY")]
public sealed class WeiCaoCaoChoiceZhouXiDexterity : WeiCaoCaoDesignChoice;
[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_CHOICE_ZHOU_XI_STEAL")]
public sealed class WeiCaoCaoChoiceZhouXiSteal : WeiCaoCaoDesignChoice;
[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_CHOICE_ZHOU_XI_DAMAGE")]
public sealed class WeiCaoCaoChoiceZhouXiDamage() : WeiCaoCaoDesignChoice(target: TargetType.AnyEnemy);

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_XIA_HOU_EN_SHI_WU")]
public sealed class WeiCaoCaoXiaHouEnShiWu() : WeiCaoCaoDesignCard(1, CardType.Attack, CardRarity.Uncommon,
    TargetType.AnyEnemy, hasJunHeEnhancement: true)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue).Targeting(play.Target!).Execute(context);
        if (!WeiCaoCaoDesignRules.Alive(this)) return;
        var attacks = PileType.Draw.GetPile(Owner).Cards.Where(card => card.Type == CardType.Attack).ToArray();
        if (attacks.Length == 0) return;

        var card = Owner.RunState.Rng.CombatCardGeneration.NextItem(attacks)!;
        var result = await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Bottom, this);
        if (result.success && result.cardAdded.IsUpgradable &&
            await WeiCaoCaoJunHePower.TryConsume(context, Owner.Creature, 1, this))
            CardCmd.Upgrade(result.cardAdded);
    }
}

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_ZHAO_YAN_BING_JI")]
public sealed class WeiCaoCaoZhaoYanBingJi() : WeiCaoCaoDesignCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(20, ValueProp.Move)];
    protected override bool IsPlayable => Owner != null && PileType.Hand.GetPile(Owner).Cards
        .Where(card => card != this).Select(card => card.Type).Distinct().Take(2).Count() < 2;
    protected override bool ShouldGlowGoldInternal => Pile?.Type == PileType.Hand && CanPlay();
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(10);

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue).Targeting(play.Target!).Execute(context);
}

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_YUE_CHEN_PO_RUI")]
public sealed class WeiCaoCaoYueChenPoRui() : WeiCaoCaoDesignCard(3, CardType.Power, CardRarity.Uncommon,
    TargetType.Self, hasJunHeEnhancement: true)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DamagePerDiscard", 5)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<WeiCaoCaoPoRuiPower>());
    protected override void OnUpgrade() => DynamicVars["DamagePerDiscard"].UpgradeValueBy(2);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var power = await PowerCmd.Apply<WeiCaoCaoPoRuiPower>(context, Owner.Creature,
            DynamicVars["DamagePerDiscard"].IntValue, Owner.Creature, this);
        if (power != null && await WeiCaoCaoJunHePower.TryConsume(context, Owner.Creature, 1, this))
            power.Enhanced = true;
    }
}

[RegisterPower]
public sealed class WeiCaoCaoPoRuiPower : WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/po_rui_buff.png";
    [SavedProperty] public bool Enhanced { get; set; }
    public override PowerStackType StackType => PowerStackType.Counter;
    private int MaxHits => Enhanced ? 7 : 5;
    public override LocString Description
    {
        get
        {
            var description = base.Description;
            description.Add("MaxHits", MaxHits);
            return description;
        }
    }

    public override async Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.IsDead || Owner.Player is not { } player || Owner.CombatState is not { } state) return;
        int count = CombatManager.Instance.History.Entries
            .OfType<CardDiscardedEntry>()
            .Count(entry => entry.Actor == Owner && entry.HappenedThisTurn(state));
        for (int i = 0; i < Math.Min(count, MaxHits) && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            var enemies = state.HittableEnemies.ToArray();
            if (enemies.Length == 0 || Owner.IsDead) break;
            var target = player.RunState.Rng.CombatTargets.NextItem(enemies)!;
#if GAME_BETA
            await CreatureCmd.Damage(context, target, Amount, ValueProp.Unpowered, Owner, null, null);
#else
            await CreatureCmd.Damage(context, target, Amount, ValueProp.Unpowered, Owner, null);
#endif
        }
    }
}

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_WEN_QIN_GUANG_AO")]
public sealed class WeiCaoCaoWenQinGuangAo() : WeiCaoCaoDesignCard(1, CardType.Attack, CardRarity.Common,
    TargetType.AllEnemies, hasJunHeEnhancement: true)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool enhanced = await WeiCaoCaoJunHePower.TryConsume(context, Owner.Creature, 1, this);
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue).TargetingAllOpponents(CombatState).Execute(context);
        if (enhanced && WeiCaoCaoDesignRules.Alive(this))
            await CardPileCmd.Draw(context, Owner);
    }
}
