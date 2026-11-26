using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 卧龙诸葛-看破：1费罕见技能牌。获得1层人工制品（抵消下一次debuff）和{Star}颗星。升级后获得2颗星。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class WoLongZhuGeLiangKanPo : WuZhugeliangCard
{
    private const string StarKey = "Star";

    public WoLongZhuGeLiangKanPo()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    // 蜀+武将 tag
    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArtifactPower>(), HoverTipFactory.FromPower<XingPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(10, ValueProp.Move), new DynamicVar(StarKey, 1m)];

    public override bool GainsBlock => true;

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(WoLongZhuGeLiangKanPo).Assembly.Location)!,
        "Voice", "Bank", "Assets", "kan_po.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);

        // 获得1层人工制品（抵消一次debuff）
        await PowerCmd.Apply<ArtifactPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 获得星（1→2）
        var stars = (int)DynamicVars[StarKey].BaseValue;
        await XingPower.Gain(choiceContext, Owner.Creature, stars, Owner.Creature, this);
    }

    /// <summary>升级：格挡 10→15，星 1颗→2颗。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(5);
        DynamicVars[StarKey].UpgradeValueBy(1);
    }
}
