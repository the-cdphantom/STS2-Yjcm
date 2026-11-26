using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using Yjcm.Extensions;

namespace Yjcm;

/// <summary>谋庞统-寅谋-阳：1费罕见技能牌，转换技、奇巧。获得2→3点能量。</summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class MouPangTongYinMouYang : WuZhugeliangCard
{
    private const string EnergyKey = "Energy";

    public MouPangTongYinMouYang()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [YjcmKeywords.ZhuanHuanJi, CardKeyword.Sly];

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new EnergyVar(EnergyKey, 2)];

    // 寅谋阳与宏策共用用户指定的宏策卡面。
    public override string CustomPortraitPath => "hong_ce.png".CardImagePath();

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(MouPangTongYinMouYang).Assembly.Location)!,
        "Voice", "Bank", "Assets", "yin_mou_yang.wav");

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromKeyword(YjcmKeywords.ZhuanHuanJi),
         HoverTipFactory.FromCard<MouPangTongYinMouYin>(IsUpgraded)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        await PlayerCmd.GainEnergy(DynamicVars[EnergyKey].BaseValue, Owner);

        var yinCard = CombatState.CreateCard<MouPangTongYinMouYin>(Owner);
        if (IsUpgraded) yinCard.UpgradeInternal();
        await CardPileCmd.AddGeneratedCardToCombat(yinCard, PileType.Draw, Owner, CardPilePosition.Random);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[EnergyKey].UpgradeValueBy(1);
    }
}
