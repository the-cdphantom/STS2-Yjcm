using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// �ӷ���-��ͫ��3�Ѽ����ƣ����ɡ��ӳ��ƶ������ù���/����/�����Ƹ�һ�ţ���������ӱ���
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class PangFengYiYiTong : WuZhugeliangCard
{
    public PangFengYiYiTong()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Sly];

    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(PangFengYiYiTong).Assembly.Location)!,
        "Voice", "Bank", "Assets", "pang_feng_yi_yi_tong.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);

        var types = new[] { CardType.Attack, CardType.Skill, CardType.Power };
        foreach (var type in types)
        {
            var candidates = PileType.Draw.GetPile(Owner).Cards
                .Concat(PileType.Discard.GetPile(Owner).Cards)
                .Where(c => c.Type == type).ToList();
            if (candidates.Count == 0) continue;

            var card = Owner.RunState.Rng.CombatCardSelection.NextItem(candidates);
            if (card == null) continue;

            // 原生命令会跳过已升满/不可升级的牌，并完成升级状态刷新。
            // 仍正常获得选中的牌，不能因为无法升级而漏抽这一类型。
            CardCmd.Upgrade(card);
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Random, this, false);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
