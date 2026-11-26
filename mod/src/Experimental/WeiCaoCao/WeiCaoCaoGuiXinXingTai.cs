using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

#nullable enable
namespace Yjcm;

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_GUI_XIN_XING_TAI")]
public sealed class WeiCaoCaoGuiXinXingTai() : WeiCaoCaoCard(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override string CustomPortraitPath => "res://Experimental/WeiCaoCao/images/gui_xin_xing_tai.png";
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<WeiCaoCaoGuiXinPower>()];

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        PowerCmd.Apply<WeiCaoCaoGuiXinPower>(context, Owner.Creature, 1, Owner.Creature, this);

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}

[RegisterPower]
public sealed class WeiCaoCaoGuiXinPower : WeiCaoCaoDesignPower
{
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/gui_xin_buff.png";

    public override Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? source) =>
        target == Owner && result.UnblockedDamage > 0 ? OnTriggered(context, null) : Task.CompletedTask;

    internal async Task OnTriggered(PlayerChoiceContext context, CardModel? source)
    {
        if (Owner.IsDead || Owner.Player is not { } player || Owner.CombatState is not { } state) return;
        int count = Amount;
        for (int i = 0; i < count && !CombatManager.Instance.IsOverOrEnding && Owner.IsAlive; i++)
        {
            var candidates = state.HittableEnemies.Select(enemy => (Enemy: enemy, Powers: enemy.Powers
                .Where(power => WeiDongZhuoGuangYongBase.CanStealOne(power, Owner)).ToArray()))
                .Where(candidate => candidate.Enemy.Block > 0 || candidate.Powers.Length > 0).ToArray();
            if (candidates.Length == 0) return;
            var candidate = player.RunState.Rng.CombatTargets.NextItem(candidates);
            var card = source ?? ChoiceSource;
            if (candidate.Powers.Length > 0)
            {
                var power = player.RunState.Rng.CombatTargets.NextItem(candidate.Powers)!;
                await WeiDongZhuoGuangYongBase.StealOne(context, power, card);
            }
            var enemy = candidate.Enemy;
            if (!enemy.IsAlive || !Owner.IsAlive) continue;
            int before = enemy.Block;
            int stolen = Math.Min(before, 6);
            if (stolen == 0) continue;
#if GAME_BETA
            await CreatureCmd.LoseBlock(context, enemy, stolen, null);
#else
            await CreatureCmd.LoseBlock(enemy, stolen);
#endif
            stolen = Math.Min(stolen, before - enemy.Block);
            if (stolen > 0 && Owner.IsAlive)
                await CreatureCmd.GainBlock(Owner, stolen, ValueProp.Unpowered, null);
        }
    }
}
