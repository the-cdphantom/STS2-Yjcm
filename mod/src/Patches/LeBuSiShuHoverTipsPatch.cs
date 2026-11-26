using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Yjcm;

/// <summary>Hide only LeBuSiShu's implicit Ethereal companion tip, not a real Exhaust keyword.</summary>
[HarmonyPatch(typeof(CardModel), "get_HoverTips")]
public static class LeBuSiShuHoverTipsPatch
{
    public static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is not (LeBuSiShu or WeiDongZhuoLeBuSiShu or WeiCaoCaoLeBuSiShu)) return;
        // Effects from another card/relic may genuinely add Exhaust. Keep that explanation.
        if (__instance.Keywords.Contains(CardKeyword.Exhaust)) return;
        var exhaustTip = HoverTipFactory.FromKeyword(CardKeyword.Exhaust);
        __result = __result.Where(tip => !tip.Equals(exhaustTip)).ToList();
    }
}
