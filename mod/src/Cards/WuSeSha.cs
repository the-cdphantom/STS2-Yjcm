using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using Yjcm.Extensions;

#nullable enable

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 无色杀：0费攻击牌，Token。对所有敌人造成{Dmg}点伤害，消耗
/// </summary>
[RegisterCard(typeof(TokenCardPool))]
public sealed class WuSeSha : CardModel
{
    private const string DmgKey = "Dmg";

    public override string PortraitPath => "wu_se_sha.png".CardImagePath();

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(DmgKey, 9m, ValueProp.Move)];

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public WuSeSha()
        : base(0, CardType.Attack, CardRarity.Token, TargetType.AllEnemies, shouldShowInCardLibrary: false) { }
    public override bool CanBeGeneratedInCombat => false;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var shaVoice = Path.Combine(
            Path.GetDirectoryName(typeof(WuSeSha).Assembly.Location)!,
            "Voice", "Bank", "Assets", "sha.wav");
        VoicePlayer.PlayWav(shaVoice);

        await ShenCaiJudgmentPower.IncrementJudgments(choiceContext, Owner, this);

        await DamageCmd.Attack(DynamicVars[DmgKey].BaseValue)
#if GAME_BETA
            .FromCard(this, cardPlay).TargetingAllOpponents(CombatState).Execute(choiceContext);
#else
            .FromCard(this).TargetingAllOpponents(CombatState).Execute(choiceContext);
#endif
    }

    protected override void OnUpgrade()
    {
        DynamicVars[DmgKey].UpgradeValueBy(3); // 9→12
    }
}
