using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 神姜维-天任：1费稀有能カ牌。每使用一张武将牌获得1层天任，层数大于体力上限/7时清零并+1体力上限+1体力。
/// 升级后获得固有。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class ShenJiangWeiTianRen : WuZhugeliangCard
{
    public ShenJiangWeiTianRen()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [];

    public override IEnumerable<CardTag> Tags =>
        SunqianMechanicBoundTag.With(base.Tags);

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.ShenJiang];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(ShenJiangWeiTianRen).Assembly.Location)!,
        "Voice", "Bank", "Assets", "tian_ren.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        await PowerCmd.Apply<TianRenPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
