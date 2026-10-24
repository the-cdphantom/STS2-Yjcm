using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 星夏侯霸-绝伐：1费罕见能力牌。失去或获得星时，对随机敌人造成{Damage}点伤害。
/// 升级后4→7。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class XingXiaHouBaJueFa : WuZhugeliangCard
{
    public XingXiaHouBaJueFa()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [];

    public override IEnumerable<CardTag> Tags =>
        SunqianMechanicBoundTag.With(base.Tags);

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<XingPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(4, ValueProp.Move)];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(XingXiaHouBaJueFa).Assembly.Location)!,
        "Voice", "Bank", "Assets", "jue_fa.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        var p = await PowerCmd.Apply<JueFaPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        // 战斗结束或无法获得能力时，不再查找不存在的能力实例。
        if (p == null) return;
        p.DamagePerTrigger = (int)DynamicVars.Damage.BaseValue;
    }

    /// <summary>升级：4→7</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
    }
}
