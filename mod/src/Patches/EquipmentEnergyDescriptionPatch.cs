using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Yjcm;

// Native OrbModel.Description has no variables. Our equipment descriptions use
// the same energy formatter as cards, including when viewed outside combat.
[HarmonyPatch(typeof(OrbModel), "get_Description")]
internal static class EquipmentEnergyDescriptionPatch
{
    private static void Postfix(OrbModel __instance, LocString __result)
    {
        if (__instance is not EquipmentOrbModel) return;
        var prefix = __instance.IsMutable && __instance.Owner != null
            ? __instance.Owner.Character.CardPool.EnergyColorName : "ironclad";
        __result.Add("energyPrefix", prefix);
    }
}
