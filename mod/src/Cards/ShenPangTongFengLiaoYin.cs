using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.HoverTips;
using Yjcm.Extensions;

#nullable enable

using STS2RitsuLib.Interop.AutoRegistration;
namespace Yjcm;

/// <summary>
/// 神庞统-凤燎-阴：Token攻击牌，全体连环+伤害（由阳生成）
/// </summary>
[RegisterCard(typeof(TokenCardPool))]
public sealed class ShenPangTongFengLiaoYin : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];
    public override string PortraitPath =>
        "shen_pang_tong_feng_liao_yin.png".CardImagePath();

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [YjcmKeywords.ZhuanHuanJi];

    // 连环 + 转换技 + 神将悬浮提示
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LianHuanPower>(),
         HoverTipFactory.FromKeyword(YjcmKeywords.ZhuanHuanJi),
         WuJiangHoverTip.ForFaction(YjcmCardTags.ShenJiang)];

    public ShenPangTongFengLiaoYin()
        : base(1, CardType.Attack, CardRarity.Token, TargetType.AllEnemies, shouldShowInCardLibrary: false) { }
    public override bool CanBeGeneratedInCombat => false;

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(ShenPangTongFengLiaoYin).Assembly.Location)!,
        "Voice", "Bank", "Assets", "shen_pang_tong_feng_liao_yin.wav");

    // 在卡牌打出前标记消耗，实现「转换技」消耗效果但不显示消耗标签
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);

        var enemies = CombatState.HittableEnemies.ToList();
        foreach (var enemy in enemies)
            await PowerCmd.Apply<LianHuanPower>(choiceContext, enemy, 5, Owner!.Creature, this, false);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
#if GAME_BETA
            .FromCard(this, cardPlay).TargetingAllOpponents(CombatState).Execute(choiceContext);
#else
            .FromCard(this).TargetingAllOpponents(CombatState).Execute(choiceContext);
#endif

        // 阴牌打出后会被消耗，必须显式生成下一张阳牌，才能继续维持阴阳交替。
        var yangCard = Owner!.Creature.CombatState.CreateCard<ShenPangTongFengLiaoYang>(Owner);
        if (IsUpgraded) yangCard.UpgradeInternal();
        await CardPileCmd.AddGeneratedCardToCombat(yangCard, PileType.Draw, Owner, CardPilePosition.Random);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.SetThisCombat(0);
    }
}
