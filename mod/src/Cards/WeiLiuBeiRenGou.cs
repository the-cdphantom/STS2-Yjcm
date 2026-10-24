using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 威刘备-仁彀：1费先古技能牌。从3张(蜀+武将)或(神+武将)牌中选一张加入手牌，本回合免费打出。
/// 由激将先古化而来。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class WeiLiuBeiRenGou : WuZhugeliangCard
{
    public WeiLiuBeiRenGou()
        : base(1, CardType.Skill, CardRarity.Ancient, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [];

    public override IEnumerable<CardTag> Tags =>
        SunqianMechanicBoundTag.With(base.Tags);

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(WeiLiuBeiRenGou).Assembly.Location)!,
        "Voice", "Bank", "Assets", "ren_gou.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);

        var pool = Owner.Character.CardPool.GetUnlockedCards(
            Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint);

        // (蜀+武将) 或 (神+武将)
        var candidates = pool.Where(c =>
            c.Tags.Contains(YjcmCardTags.WuJiang)
            && (c.Tags.Contains(YjcmCardTags.Shu) || c.Tags.Contains(YjcmCardTags.ShenJiang))
            && c != this
        ).ToList();

        if (candidates.Count == 0) return;

        var count = Math.Min(5, candidates.Count);
        var selected = CardFactory.GetDistinctForCombat(Owner, candidates,
            count, Owner.RunState.Rng.CombatCardGeneration).ToList();

        // 有可用稀有武将时，确保五张候选中至少出现一张稀有牌。
        var rareCandidates = candidates.Where(c => c.Rarity == CardRarity.Rare).ToList();
        if (rareCandidates.Count > 0 && selected.All(c => c.Rarity != CardRarity.Rare))
        {
            var rare = CardFactory.GetDistinctForCombat(Owner, rareCandidates, 1,
                Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
            if (rare != null && selected.Count > 0)
                selected[0] = rare;
        }

        if (!selected.Any()) return;

        var chosen = await CardSelectCmd.FromSimpleGrid(
            choiceContext, selected, Owner,
            new CardSelectorPrefs(
                new MegaCrit.Sts2.Core.Localization.LocString("cards", "YJCM_CARD_WEI_LIU_BEI_REN_GOU.select_prompt"),
                1, 1));

        var card = chosen.FirstOrDefault();
        if (card != null)
        {
            card.SetToFreeThisTurn();
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
        }
    }

    /// <summary>升级减1费（1→0）</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
