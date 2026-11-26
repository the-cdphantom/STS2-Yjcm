using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 神姜维-平襄：0费稀有技能牌。失去9点体力上限，将随机{Count}张0费攻击牌（来自任意角色）加入手牌并附加虚无+消耗。
/// 升级后{Count}从7→9。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class ShenJiangWeiPingXiang : WuZhugeliangCard
{
    private const string CountKey = "Count";

    public ShenJiangWeiPingXiang()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Ethereal, CardKeyword.Exhaust];

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.ShenJiang];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar(CountKey, 7m)];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(ShenJiangWeiPingXiang).Assembly.Location)!,
        "Voice", "Bank", "Assets", "ping_xiang.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);

        // 失去9点体力上限
        await YjcmCreatureCompat.LoseMaxHp(choiceContext, Owner.Creature, 9, this);

        // 收集所有攻击牌（不限费用）
        var allCards = ModelDb.AllCards
            .Where(c => c.Type == CardType.Attack && !c.EnergyCost.CostsX)
            .ToList();

        if (allCards.Count == 0) return;

        var count = (int)DynamicVars[CountKey].BaseValue;
        var selected = CardFactory.GetDistinctForCombat(Owner, allCards,
            Math.Min(count, allCards.Count), Owner.RunState.Rng.CombatCardGeneration);

        foreach (var card in selected)
        {
            card.SetToFreeThisTurn();
            CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
            CardCmd.ApplyKeyword(card, CardKeyword.Exhaust);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[CountKey].UpgradeValueBy(2); // 7 → 9
    }
}
