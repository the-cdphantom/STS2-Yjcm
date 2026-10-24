using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 星夏侯霸-维谷：2费罕见能力牌。使用单目标攻击牌时，可弃置一张以玩家为目标的牌，获得1颗星。
/// 升级后1费。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class XingXiaHouBaWeiGu : WuZhugeliangCard
{
    public XingXiaHouBaWeiGu()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [];

    public override IEnumerable<CardTag> Tags =>
        SunqianMechanicBoundTag.With(base.Tags);

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<XingPower>()];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(XingXiaHouBaWeiGu).Assembly.Location)!,
        "Voice", "Bank", "Assets", "wei_gu.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        await PowerCmd.Apply<WeiGuPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
    }

    /// <summary>升级：2费→1费</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
