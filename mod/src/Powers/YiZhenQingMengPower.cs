using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Yjcm.Interop;

#nullable enable

namespace Yjcm;

/// <summary>
/// 一枕清梦：回合结束时，所有敌人回复剩余火攻/灼烧层数的生命并失去该能力，然后此buff消失。
/// </summary>
public sealed class YiZhenQingMengPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override string CustomIconPath => "res://Yjcm/Images/Powers/yi_zhen_qing_meng.png";
    public override string CustomBigIconPath => "res://Yjcm/Images/Powers/yi_zhen_qing_meng.png";

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEnd(choiceContext, side, participants);
        if (side != CombatSide.Player) return;
        if (Owner?.CombatState == null) return;
        bool useBurning = YiZhenQingMengInterop.Affects(Owner.Player);

        foreach (var enemy in Owner.CombatState.HittableEnemies)
        {
            var effect = YiZhenQingMengInterop.FindEffect(enemy, useBurning);
            if (effect == null || effect.Amount <= 0) continue;

            var healAmt = effect.Amount;
            await CreatureCmd.Heal(enemy, healAmt);
            await PowerCmd.Remove(effect);
        }

        // 自身消失
        await PowerCmd.Remove(this);
    }
}
