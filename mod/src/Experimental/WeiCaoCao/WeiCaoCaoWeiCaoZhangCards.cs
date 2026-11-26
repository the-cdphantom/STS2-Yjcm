using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_WEI_CAO_ZHANG_NING_PAO")]
public sealed class WeiCaoCaoWeiCaoZhangNingPao() : WeiCaoCaoDesignCard(1, CardType.Skill, CardRarity.Rare,
    TargetType.Self, hasJunHeEnhancement: true)
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<BufferPower>());

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var selected = await WeiCaoCaoDesignRules.Hand(context, this, 0, 2,
            prompt: "ning_pao_discard", forDiscard: true);
        var discarded = new List<CardModel>();
        foreach (var card in selected)
        {
            if (!WeiCaoCaoDesignRules.Alive(this)) return;
            if (card.Pile?.Type == PileType.Hand) discarded.Add(card);
        }
        if (discarded.Count > 0) await CardCmd.Discard(context, discarded);
        if (!WeiCaoCaoDesignRules.Alive(this)) return;

        int drawn = (await CardPileCmd.Draw(context, discarded.Count, Owner, false)).Count();
        await GrantBuffer(context, Owner);
        foreach (var teammate in CombatState.Players.Where(player => player != Owner && player.Creature.IsAlive).ToArray())
        {
            if (teammate.Creature.IsDead || !WeiCaoCaoDesignRules.Alive(this)) continue;
            await CardPileCmd.Draw(context, drawn, teammate, false);
            await GrantBuffer(context, teammate);
        }
        if (WeiCaoCaoDesignRules.Alive(this) &&
            await WeiCaoCaoJunHePower.TryConsume(context, Owner.Creature, 1, this))
            await PlayerCmd.GainEnergy(1, Owner);
    }

    private Task GrantBuffer(PlayerChoiceContext context, Player player) =>
        player.Creature.IsAlive ? PowerCmd.Apply<BufferPower>(context, player.Creature, 1, Owner.Creature, this) : Task.CompletedTask;
}

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_WEI_CAO_ZHANG_KUI_PAN")]
public sealed class WeiCaoCaoWeiCaoZhangKuiPan() : WeiCaoCaoDesignCard(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override HashSet<CardTag> CanonicalTags => [.. base.CanonicalTags, WeiDongZhuoComboTags.LianZhaoJi];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Concat([
        new HoverTip(new LocString("cards", "YJCM_CAO_CAO_COMBO_POWER.title"),
            new LocString("cards", "YJCM_CAO_CAO_COMBO_POWER.description")),
        HoverTipFactory.Static(StaticHoverTip.ReplayDynamic, new DynamicVar("Times", 1))
    ]);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Target is not { IsAlive: true } target) return;
        bool combo = WeiDongZhuoComboHistory.ImmediatelyFollows(play, CardType.Power);
        if (combo)
        {
            WeiDongZhuoComboHistory.RecordTriggered(play);
            _ = WeiDongZhuoComboVfx.Play(WeiDongZhuoComboVfxStyle.KuiPan);
        }
        await WeiDongZhuoQunEffects.Attack(this, play, DynamicVars.Damage.BaseValue).Targeting(target).Execute(context);
        if (!combo || Owner.Creature.IsDead) return;

        Creature[] affected = target.IsAlive ? [Owner.Creature, target] : [Owner.Creature];
#if GAME_BETA
        await CreatureCmd.Damage(context, affected, 6, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
#else
        await CreatureCmd.Damage(context, affected, 6, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
#endif
        if (!WeiCaoCaoDesignRules.Alive(this)) return;

        int toDraw = CardPile.MaxCardsInHand - PileType.Hand.GetPile(Owner).Cards.Count;
        if (toDraw > 0) await CardPileCmd.Draw(context, toDraw, Owner, false);
        var chosen = (await WeiCaoCaoDesignRules.Hand(context, this, 1, 1,
            card => card.Type == CardType.Attack && !card.Keywords.Contains(CardKeyword.Unplayable),
            prompt: "kui_pan_attack", releaseSelectionOnConfirm: true)).FirstOrDefault();
        if (chosen == null || !WeiCaoCaoDesignRules.Alive(this)) return;
        var (confirmed, chosenTarget) = await WeiCaoCaoFuPlay.SelectTarget(context, chosen, target);
        if (!confirmed || chosen.Pile?.Type != PileType.Hand || !WeiCaoCaoDesignRules.Alive(this)) return;
        int replays = CombatState.Players.Count(player => player != Owner && player.Creature.IsAlive);
        await WeiCaoCaoFuPlay.PlayWithExtraReplays(context, chosen, chosenTarget, replays);
    }
}
