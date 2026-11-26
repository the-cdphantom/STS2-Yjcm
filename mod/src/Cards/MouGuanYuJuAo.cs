using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 谋关羽-倨傲：1费普通攻击牌。弃置一张手牌，造成{Damage}点伤害。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class MouGuanYuJuAo : WuZhugeliangCard
{
    public MouGuanYuJuAo()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    /// <summary>武将+蜀标签</summary>
    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    /// <summary>基础伤害12，升级至15</summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(12, ValueProp.Move)];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(MouGuanYuJuAo).Assembly.Location)!,
        "Voice", "Bank", "Assets", "ju_ao.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);

        // 弃置一张手牌
        var handPile = PileTypeExtensions.GetPile(PileType.Hand, Owner);
        if (handPile?.Cards != null && handPile.Cards.Count > 0)
        {
            var selected = await CardSelectCmd.FromHandForDiscard(
                choiceContext, Owner,
                new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
                card => card != this, this);
            var card = selected.FirstOrDefault();
            if (card != null)
                await CardCmd.Discard(choiceContext, card);
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
#if GAME_BETA
            .FromCard(this, cardPlay).Targeting(cardPlay.Target!).Execute(choiceContext);
#else
            .FromCard(this).Targeting(cardPlay.Target!).Execute(choiceContext);
#endif
    }

    /// <summary>升级：伤害12→15</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
    }
}
