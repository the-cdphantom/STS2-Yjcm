using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 星法正-知机：1费罕见技能牌。
/// 弃置任意张手牌（可以不弃），将手牌摸至5张。
/// 摸的牌多→本回合结束保留所有手牌；
/// 数量相等→本回合造成伤害时无视格挡；
/// 弃的牌多→获得1点能量并对所有敌人造成弃牌数×{DmgPerDiscard}伤害。
/// 升级后费用变为0。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class XingFaZhengZhiJi : WuZhugeliangCard
{
    private const string DmgKey = "DmgPerDiscard";

    public XingFaZhengZhiJi()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    /// <summary>无额外关键词</summary>
    public override HashSet<CardKeyword> CanonicalKeywords => [];

    /// <summary>武将+蜀标签</summary>
    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    /// <summary>卡面显示每弃牌数的伤害倍率</summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar(DmgKey, 3)];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(XingFaZhengZhiJi).Assembly.Location)!,
        "Voice", "Bank", "Assets", "zhi_ji.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);

        var handPile = PileTypeExtensions.GetPile(PileType.Hand, Owner);
        var currentHandSize = handPile?.Cards.Count ?? 0;

        // 让玩家选择要弃置的牌（可弃完所有其他手牌）
        var maxDiscard = currentHandSize;
        CardModel[] discarded = [];
        if (maxDiscard > 0)
        {
            var selected = await CardSelectCmd.FromHandForDiscard(
                choiceContext, Owner,
                new CardSelectorPrefs(
                    new MegaCrit.Sts2.Core.Localization.LocString("cards", "YJCM_CARD_XING_FA_ZHENG_ZHI_JI.select_prompt"),
                    0, maxDiscard),
                card => card != this, this);
            discarded = selected.ToArray();
        }

        // 弃置选中的牌
        foreach (var card in discarded)
            await CardCmd.Discard(choiceContext, card);

        var discardCount = discarded.Length;
        var handAfterDiscard = PileTypeExtensions.GetPile(PileType.Hand, Owner)
            ?.Cards.Count ?? 0;
        var drawCount = Math.Max(0, 5 - handAfterDiscard);

        // 将手牌摸至5张
        if (drawCount > 0)
            await CardPileCmd.Draw(choiceContext, drawCount, Owner, false);

        // 根据摸牌与弃牌数量比较触发效果
        if (drawCount > discardCount)
        {
            // 摸的牌多：本回合结束保留所有手牌
            await PowerCmd.Apply<ZhiJiRetainPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        }
        else if (drawCount == discardCount)
        {
            // 数量相等：本回合造成伤害时无视格挡
            await PowerCmd.Apply<ZhiJiPiercePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        }
        else
        {
            // 弃的牌多：获得1能量 + 对所有敌人造成弃牌数×倍率伤害
            await PlayerCmd.GainEnergy(1, Owner);
            var dmgPerDiscard = (int)DynamicVars[DmgKey].BaseValue;
            var totalDmg = discardCount * dmgPerDiscard;
            var enemies = CombatState.HittableEnemies;
            foreach (var enemy in enemies)
#if GAME_BETA
                await CreatureCmd.Damage(choiceContext, enemy, totalDmg, ValueProp.Move, this, cardPlay);
#else
                await CreatureCmd.Damage(choiceContext, enemy, totalDmg, ValueProp.Move, this);
#endif
        }
    }

    /// <summary>升级：1费变为0费</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
