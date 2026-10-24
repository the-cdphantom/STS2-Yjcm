using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 威张星彩-贤贶：1费稀有能力牌。
/// 每回合有牌第一次被弃置时，可以废除一个装备栏，摸已废除装备栏数量的牌。
/// 升级后0费。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class WeiZhangXingCaiXianKuang : WuZhugeliangCard
{
    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(WeiZhangXingCaiXianKuang).Assembly.Location)!,
        "Voice", "Bank", "Assets", "xian_kuang.wav");

    public WeiZhangXingCaiXianKuang()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    public override IEnumerable<CardTag> Tags =>
        SunqianMechanicBoundTag.With(base.Tags);

    // 武将+蜀 tag：纳入武将牌相关机制（天任、尽瘁等）
    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        var owner = cardPlay.Card.Owner;
        if (owner == null) return;

        // 施加贤贶能力（层数1，标记本回合已拥有能力）
        await PowerCmd.Apply<WeiZhangXingCaiXianKuangPower>(choiceContext,
            owner.Creature, 1, owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1); // 1→0
    }
}
