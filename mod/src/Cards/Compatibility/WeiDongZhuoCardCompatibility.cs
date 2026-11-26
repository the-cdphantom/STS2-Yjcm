using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable

namespace Yjcm;

// The host's existing assembly PatchAll discovery finds these classes automatically.
// Every postfix adds clone-only behavior; original cards keep their original result.
[HarmonyPatch(typeof(ModCardTemplate), "get_ExtraHoverTips")]
public static class WeiDongZhuoWuJiangHoverTipsPatch
{
    public static void Postfix(ModCardTemplate __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is not WeiDongZhuoCard card || !card.TryGetWuJiangHoverTip(out var tip)) return;
        __result = __result.Concat(new[] { tip }).ToList();
    }
}

// Keep the existing tracker shared, so either WuSeSha type increments the same combat count.
// The original private method synchronizes original ShenCai instances; append only our copies.
[HarmonyPatch(typeof(ShenCaiJudgmentPower), "SyncCardVariables")]
public static class WeiDongZhuoShenCaiVariableSyncPatch
{
    private static readonly PileType[] CombatPiles =
        [PileType.Draw, PileType.Hand, PileType.Discard, PileType.Exhaust, PileType.Play];

    public static void Postfix(Player player, int count)
    {
        foreach (var pileType in CombatPiles)
        {
            var pile = PileTypeExtensions.GetPile(pileType, player);
            if (pile?.Cards == null) continue;
            foreach (var card in pile.Cards.OfType<WeiDongZhuoShenZhangFeiShenCai>())
                card.SetJudgmentCount(count);
        }
    }
}
