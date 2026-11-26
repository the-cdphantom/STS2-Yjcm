using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 威刘备-烈骧：X费稀有攻击牌。造成{Damage}点伤害X次，失去X点体力，
/// 使敌人失去X点力量，下回合抽X张牌。升级后5→10伤。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class WeiLiuBeiLieXiang : WuZhugeliangCard
{
    public WeiLiuBeiLieXiang()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [];

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(10, ValueProp.Move)];

    // 为描述中的 [力量] 提供悬浮提示
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<StrengthPower>()];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(WeiLiuBeiLieXiang).Assembly.Location)!,
        "Voice", "Bank", "Assets", "lie_xiang.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        var x = ResolveEnergyXValue();
        if (x <= 0) return;

        // 造成伤害X次
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
#if GAME_BETA
            .WithHitCount(x).FromCard(this, cardPlay)
#else
            .WithHitCount(x).FromCard(this)
#endif
            .Targeting(cardPlay.Target!).Execute(choiceContext);

        // 失去X点体力
        await CreatureCmd.Damage(choiceContext, Owner.Creature, x,
            ValueProp.Unblockable | ValueProp.Unpowered, null, null);

        // 敌人失去X点力量
        await PowerCmd.Apply<StrengthPower>(choiceContext, cardPlay.Target!, -x, Owner.Creature, this, false);

        // 下回合抽X张牌
        await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, Owner.Creature, x, Owner.Creature, this, false);
    }

    /// <summary>升级：10→15</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5);
    }
}
