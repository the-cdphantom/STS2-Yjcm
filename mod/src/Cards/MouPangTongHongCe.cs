using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using Yjcm.Extensions;

namespace Yjcm;

/// <summary>谋庞统-宏策：1费罕见技能牌。固有，消耗。选择一张宏策加入手牌。</summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class MouPangTongHongCe : WuZhugeliangCard
{
    public MouPangTongHongCe()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Innate, CardKeyword.Exhaust];

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    // 母牌与三张宏策衍生牌共用用户提供的宏策卡面。
    public override string CustomPortraitPath => "hong_ce.png".CardImagePath();

    // 在卡牌详情中直接显示三张可生成的宏策。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<HongCeShangCe>(),
         HoverTipFactory.FromCard<HongCeZhongCe>(),
         HoverTipFactory.FromCard<HongCeXiaCe>()];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(MouPangTongHongCe).Assembly.Location)!,
        "Voice", "Bank", "Assets", "hong_ce.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        var options = new CardModel[]
        {
            Owner.Creature.CombatState.CreateCard<HongCeShangCe>(Owner),
            Owner.Creature.CombatState.CreateCard<HongCeZhongCe>(Owner),
            Owner.Creature.CombatState.CreateCard<HongCeXiaCe>(Owner),
        };
        var chosen = (await CardSelectCmd.FromSimpleGrid(
            choiceContext, options, Owner,
            new CardSelectorPrefs(new LocString("cards", "hong_ce.select_prompt"), 1, 1)))
            .FirstOrDefault();
        if (chosen != null)
            await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner, CardPilePosition.Random);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class HongCeShangCe : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];
    public HongCeShangCe() : base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy, shouldShowInCardLibrary: false) { }
    public override bool CanBeGeneratedInCombat => false;
    public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();
    public override string PortraitPath => "hong_ce.png".CardImagePath();
    private static readonly string VoicePath = Path.Combine(Path.GetDirectoryName(typeof(HongCeShangCe).Assembly.Location)!, "Voice", "Bank", "Assets", "hong_ce_shang.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
#if GAME_BETA
            .FromCard(this, cardPlay).Targeting(cardPlay.Target!).Execute(choiceContext);
#else
            .FromCard(this).Targeting(cardPlay.Target!).Execute(choiceContext);
#endif
        await PowerCmd.Apply<ZhanJueFreeAttackPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class HongCeZhongCe : CardModel
{
    public HongCeZhongCe() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false) { }
    public override bool CanBeGeneratedInCombat => false;
    public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();
    public override string PortraitPath => "hong_ce.png".CardImagePath();
    private static readonly string VoicePath = Path.Combine(Path.GetDirectoryName(typeof(HongCeZhongCe).Assembly.Location)!, "Voice", "Bank", "Assets", "hong_ce_zhong.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        var hand = PileTypeExtensions.GetPile(PileType.Hand, Owner);
        var discardable = hand?.Cards.Where(card => card != this).ToList() ?? [];
        if (discardable.Count > 0)
        {
            var selected = await CardSelectCmd.FromHandForDiscard(choiceContext, Owner,
                new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
                card => card != this, this);
            var discarded = selected.FirstOrDefault();
            if (discarded != null)
                await CardCmd.Discard(choiceContext, discarded);
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }

        var skillCards = ModelDb.AllCards
            .Where(card => card.Type == CardType.Skill
                && card.Pool is WuZhugeliangCardPool
                && card.Rarity != CardRarity.Token
                && card.ShouldShowInCardLibrary)
            .ToList();
        if (skillCards.Count == 0) return;
        var options = CardFactory.GetDistinctForCombat(
            Owner, skillCards, Math.Min(3, skillCards.Count),
            Owner.RunState.Rng.CombatCardGeneration).ToList();
        var chosen = (await CardSelectCmd.FromSimpleGrid(
            choiceContext, options, Owner,
            new CardSelectorPrefs(new LocString("cards", "hong_ce_zhong.select_prompt"), 1, 1)))
            .FirstOrDefault();
        if (chosen == null) return;
        chosen.SetToFreeThisTurn();
        CardCmd.ApplyKeyword(chosen, CardKeyword.Ethereal);
        CardCmd.ApplyKeyword(chosen, CardKeyword.Exhaust);
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner, CardPilePosition.Random);
    }

    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class HongCeXiaCe : CardModel
{
    public HongCeXiaCe() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false) { }
    public override bool CanBeGeneratedInCombat => false;
    public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();
    public override string PortraitPath => "hong_ce.png".CardImagePath();
    private static readonly string VoicePath = Path.Combine(Path.GetDirectoryName(typeof(HongCeXiaCe).Assembly.Location)!, "Voice", "Bank", "Assets", "hong_ce_xia.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        await CardPileCmd.Draw(choiceContext, 3, Owner);
    }

    protected override void OnUpgrade() { }
}
