using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// ����-������1�Ѽ����ƣ������һ���佫+���Ƽ������ƣ����غ����
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class LiuBeiJiJiang : WuZhugeliangCard
{
    public LiuBeiJiJiang()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public override IEnumerable<CardTag> Tags =>
        SunqianMechanicBoundTag.With(base.Tags);

    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(LiuBeiJiJiang).Assembly.Location)!,
        "Voice", "Bank", "Assets", "liu_bei_ji_jiang.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);

        var pool = Owner.Character.CardPool.GetUnlockedCards(
            Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint);

        // 通过 Tag 过滤：需要同时有「武将」和「蜀」标签
        var shuWuJiang = pool.Where(c =>
            c.Tags.Contains(YjcmCardTags.WuJiang)
            && c.Tags.Contains(YjcmCardTags.Shu)
            && c != this
        ).ToList();

        if (shuWuJiang.Count == 0) return;

        var selected = CardFactory.GetDistinctForCombat(Owner, shuWuJiang,
            Math.Min(3, shuWuJiang.Count), Owner.RunState.Rng.CombatCardGeneration).ToList();
        if (selected.Count == 0) return;

        var chosen = await CardSelectCmd.FromSimpleGrid(
            choiceContext, selected, Owner,
            new CardSelectorPrefs(
                new MegaCrit.Sts2.Core.Localization.LocString("cards", "YJCM_CARD_LIU_BEI_JI_JIANG.select_prompt"),
                1, 1));
        var card = chosen.FirstOrDefault();
        if (card == null) return;

        card.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
