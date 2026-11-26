using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

internal static class HuoShaoLuoYangRules
{
    internal static int MaxHpLoss(int currentMaxHp, int percent) =>
        (int)Math.Floor(currentMaxHp * percent / 100m);

    internal static decimal DamagePerExhaustedCard(int actualMaxHpLost, bool upgraded) =>
        actualMaxHpLost * (upgraded ? 2m : 1m);
}

/// <summary>董卓-火烧洛阳：以生命上限和整手手牌换取逐张群体攻击。</summary>
[RegisterCard(typeof(WeiDongZhuoCardPool), FullPublicEntry = "YJCM_CARD_DONG_ZHUO_HUO_SHAO_LUO_YANG")]
public sealed class WeiDongZhuoDongZhuoHuoShaoLuoYang : WeiDongZhuoQunCard
{
    private const string MaxHpPercentKey = "MaxHpPercent";

    public WeiDongZhuoDongZhuoHuoShaoLuoYang()
        : base(3, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies) { }

    public override string CustomPortraitPath =>
        "res://WeiDongZhuo/images/huo_shao_luo_yang.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar(MaxHpPercentKey, 20)];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiDongZhuoQunEffects.PlayVoice("huo_shao_luo_yang.wav");

        int maxHpBefore = Owner.Creature.MaxHp;
        int requestedLoss = HuoShaoLuoYangRules.MaxHpLoss(
            maxHpBefore, (int)DynamicVars[MaxHpPercentKey].BaseValue);
        if (requestedLoss > 0)
            await YjcmCreatureCompat.LoseMaxHp(context, Owner.Creature, requestedLoss, this);

        int actualMaxHpLost = Math.Max(0, maxHpBefore - Owner.Creature.MaxHp);
        decimal damage = HuoShaoLuoYangRules.DamagePerExhaustedCard(
            actualMaxHpLost, IsUpgraded);

        // 快照只包含本效果开始消耗时已有的手牌；消耗途中生成的新牌不会被递归消耗。
        var handCards = PileType.Hand.GetPile(Owner).Cards.ToList();
        foreach (var card in handCards)
        {
            if (card.Pile?.Type != PileType.Hand) continue;
            await CardCmd.Exhaust(context, card, causedByEthereal: false, skipVisuals: false);
            if (card.Pile?.Type != PileType.Exhaust || damage <= 0m) continue;

            await WeiDongZhuoQunEffects.Attack(this, play, damage)
                .TargetingAllOpponents(CombatState).Execute(context);
        }
    }

    protected override void OnUpgrade() =>
        DynamicVars[MaxHpPercentKey].UpgradeValueBy(-10);
}
