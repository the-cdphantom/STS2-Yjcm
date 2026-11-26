using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 囤储：0费罕见能力牌。获得{Energy}点能量，获得囤储buff（能量保留至下回合但不再自动获得能量）。
/// 升级后{Energy}从15→20。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class TunChu : WuZhugeliangCard
{
    private const string EnergyKey = "Energy";

    public TunChu()
        : base(0, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [];

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new EnergyVar(EnergyKey, 15)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<TunChuPower>()];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(TunChu).Assembly.Location)!,
        "Voice", "Bank", "Assets", "tun_chu.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        await PlayerCmd.GainEnergy(DynamicVars[EnergyKey].BaseValue, Owner);
        await PowerCmd.Apply<TunChuPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[EnergyKey].UpgradeValueBy(5); // 15 → 20
    }
}
