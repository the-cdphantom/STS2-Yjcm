using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 火攻：1费攻击牌。使一名敌人获得{Stacks}层火攻，弃置一张手牌。升级后6→9层。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class HuoGong : WuZhugeliangCard
{
    private const string StacksKey = "Stacks";

    public HuoGong()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new HuoGongVar(StacksKey, 6m)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<HuoGongPower>()];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(HuoGong).Assembly.Location)!,
        "Voice", "Bank", "Assets", "huo_gong.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);

        var stacks = (int)DynamicVars[StacksKey].BaseValue;
        await PowerCmd.Apply<HuoGongPower>(choiceContext, cardPlay.Target!, stacks, Owner.Creature, this, false);

        var selected = await CardSelectCmd.FromHandForDiscard(
            choiceContext, Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
            card => card != this, this);

        var card = selected.FirstOrDefault();
        if (card != null)
            await CardCmd.Discard(choiceContext, card);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[StacksKey].UpgradeValueBy(3); // 6→9
    }
}
