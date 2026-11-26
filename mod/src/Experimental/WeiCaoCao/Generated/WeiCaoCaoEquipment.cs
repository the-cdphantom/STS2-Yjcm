using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace Yjcm;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class WeiCaoCaoEquipmentSetup
{
    private static void Postfix(ICombatState? combatState, ref Task __result) =>
        __result = Initialize(__result, combatState);

    private static async Task Initialize(Task original, ICombatState? state)
    {
        await original;
        if (state == null) return;
        foreach (var player in state.Players.Where(p => p.Character is WeiCaoCao))
        {
            var context = new ThrowingPlayerChoiceContext();
            var placeholders = new EquipmentOrbModel[]
            {
                (EquipmentOrbModel)ModelDb.Orb<EmptyWeaponSlotOrb>().ToMutable(),
                (EquipmentOrbModel)ModelDb.Orb<EmptyArmorSlotOrb>().ToMutable(),
                (EquipmentOrbModel)ModelDb.Orb<EmptyDefensiveHorseSlotOrb>().ToMutable(),
                (EquipmentOrbModel)ModelDb.Orb<EmptyOffensiveHorseSlotOrb>().ToMutable(),
                (EquipmentOrbModel)ModelDb.Orb<EmptyTreasureSlotOrb>().ToMutable()
            };
            foreach (var orb in placeholders)
            {
                if (!player.PlayerCombatState.OrbQueue.Orbs.OfType<EquipmentOrbModel>().Any(o => o.SlotType == orb.SlotType))
                    await EquipCmd.ChannelEquipment(context, player, orb, false);
            }
        }
    }
}

[HarmonyPatch(typeof(OrbCmd), nameof(OrbCmd.AddSlots))]
internal static class WeiCaoCaoProtectAddedSlots
{
    private static bool Prefix(Player player, ref Task __result)
    {
        if (player.Character is not WeiCaoCao) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(OrbCmd), nameof(OrbCmd.RemoveSlots))]
internal static class WeiCaoCaoProtectRemovedSlots
{
    private static bool Prefix(Player player) => player.Character is not WeiCaoCao;
}

[HarmonyPatch(typeof(OrbCmd), nameof(OrbCmd.Channel), new[] { typeof(PlayerChoiceContext), typeof(OrbModel), typeof(Player) })]
internal static class WeiCaoCaoTreasureRouting
{
    private static bool Prefix(OrbModel orb, Player player, ref Task __result)
    {
        if (player.Character is not WeiCaoCao || orb is EquipmentOrbModel) return true;
        EquipCmd.RouteToTreasure(player, orb);
        __result = Task.CompletedTask;
        return false;
    }
}
