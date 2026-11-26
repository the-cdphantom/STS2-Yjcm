using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 张翼-暮锐：1费罕见攻击牌。造成{Damage}点伤害，
/// 持有者回合开始时从抽牌堆、弃牌堆或手牌自动打出。升级后0费。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class ZhangYiMuRui : WuZhugeliangCard
{
    public ZhangYiMuRui()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    public override HashSet<CardKeyword> CanonicalKeywords =>
        [];

    protected override HashSet<CardTag> CanonicalTags =>
        [YjcmCardTags.WuJiang, YjcmCardTags.Shu];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(5, ValueProp.Move)];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(ZhangYiMuRui).Assembly.Location)!,
        "Voice", "Bank", "Assets", "mu_rui.wav");

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player) =>
        player == Owner ? MuRuiPatch.AutoPlayAtTurnStart(choiceContext, this) : Task.CompletedTask;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
#if GAME_BETA
            .FromCard(this, cardPlay).Targeting(cardPlay.Target!).Execute(choiceContext);
#else
            .FromCard(this).Targeting(cardPlay.Target!).Execute(choiceContext);
#endif
    }

    /// <summary>升级：1费→0费</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
