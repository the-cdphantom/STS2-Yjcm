using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 北伐：3费先古技能牌。失去所有星，打出牌组中所有武将牌并消耗。
/// 下回合获得{EnergyGain}能量+抽{DrawCount}牌+{StrengthGain}力量+{DexterityGain}敏捷，回合结束死亡。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class BeiFa : WuZhugeliangCard
{
    private const string EnergyKey = "EnergyGain";
    private const string DrawKey = "DrawCount";
    private const string StrKey = "StrengthGain";
    private const string DexKey = "DexterityGain";

    public BeiFa()
        : base(3, CardType.Skill, CardRarity.Ancient, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    // 北伐是蜀国武将牌：手动追加"这是蜀国的武将"+星悬浮框
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [WuJiangHoverTip.ForFaction(YjcmCardTags.Shu),
         HoverTipFactory.FromPower<XingPower>()];

    /// <summary>卡面显示的数值，升级预览自动生效</summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new EnergyVar(EnergyKey, 7),
        new DynamicVar(DrawKey, 7),
        new DynamicVar(StrKey, 77),
        new DynamicVar(DexKey, 77),
    ];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(BeiFa).Assembly.Location)!,
        "Voice", "Bank", "Assets", "bei_fa.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);

        // 失去所有星
        var xing = Owner.Creature.Powers?.OfType<XingPower>().ToList();
        if (xing != null)
            foreach (var x in xing) await PowerCmd.Remove(x);

        // 收集牌组中所有武将牌
        var wuJiangCards = new List<CardModel>();
        foreach (var pileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard })
        {
            var pile = PileTypeExtensions.GetPile(pileType, Owner);
            if (pile?.Cards != null)
            {
                foreach (var c in pile.Cards)
                    if (c.Tags.Contains(YjcmCardTags.WuJiang) && c != this)
                        wuJiangCards.Add(c);
            }
        }

        // 自动打出所有武将牌
        var enemies = CombatState.HittableEnemies.ToList();
        foreach (var card in wuJiangCards)
        {
            Creature? target = null;
            if (card.Type == CardType.Attack && enemies.Count > 0)
                target = Owner.RunState.Rng.CombatCardSelection.NextItem(enemies);
            else if (card.Type == CardType.Skill || card.Type == CardType.Power)
                target = Owner.Creature;

            if (target != null)
            {
                card.ExhaustOnNextPlay = true;
                await CardCmd.AutoPlay(choiceContext, card, target, AutoPlayType.Default, false, false);
            }
        }

        // 下回合buff：数值由 DynamicVars 驱动，升级后自动使用升级值
        var beiFa = await PowerCmd.Apply<BeiFaPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        // 连续自动出牌可能已经结束战斗，此时没有下回合能力可配置。
        if (beiFa == null) return;
        beiFa.EnergyAmount = (int)DynamicVars[EnergyKey].BaseValue;
        beiFa.CardDrawAmount = (int)DynamicVars[DrawKey].BaseValue;
        beiFa.StrengthAmount = (int)DynamicVars[StrKey].BaseValue;
        beiFa.DexterityAmount = (int)DynamicVars[DexKey].BaseValue;
    }

    /// <summary>升级：7→8能量/抽牌，77→88力量/敏捷</summary>
    protected override void OnUpgrade()
    {
        DynamicVars[EnergyKey].UpgradeValueBy(1);
        DynamicVars[DrawKey].UpgradeValueBy(1);
        DynamicVars[StrKey].UpgradeValueBy(11);
        DynamicVars[DexKey].UpgradeValueBy(11);
    }
}
