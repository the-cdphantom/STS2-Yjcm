using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

internal static class DiscardSelectionCases
{
    private static CardSelectorPrefs Captured;
    private static bool Playable;
    private static UnplayableReason Reason;

    public static async Task Run(Assembly mod, Action<bool, string> check)
    {
        var harmony = new Harmony("Yjcm.tests.discard-glow");
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        try
        {
            harmony.Patch(typeof(CardSelectCmd).GetMethod("FromHand")!, prefix:
                new HarmonyMethod(typeof(DiscardSelectionCases).GetMethod(nameof(Select), flags)!));
            harmony.Patch(typeof(CardModel).GetMethod("CanPlay", [typeof(UnplayableReason).MakeByRefType(), typeof(AbstractModel).MakeByRefType()])!,
                prefix: new HarmonyMethod(typeof(DiscardSelectionCases).GetMethod(nameof(CanPlay), flags)!));
            var prefs = new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 0, 3)
            { RequireManualConfirmation = true, Cancelable = true };
            await CardSelectCmd.FromHandForDiscard(null!, null!, prefs, null, null!);
            check(Captured.ShouldGlowGold != null, "Native discard selection must provide its gold glow predicate");
            check(Captured.MinSelect == 0 && Captured.MaxSelect == 3 && Captured.Cancelable && Captured.RequireManualConfirmation,
                "Native discard wrapper must preserve skip, count and confirmation settings");
            var insufficientResource = UnplayableReason.EnergyCostTooHigh;
            var blocked = UnplayableReason.BlockedByHook;
            foreach (string name in new[] { "HuangWudieShuangRui", "GuanPingLongYin", "LeZhuGeGuoChengYan", "LiuBaZhuBi", "LiuBaLiuZhuan",
                "MouPangTongYinMouYang", "MouPangTongYinMouYin", "PangFengYiYiTong", "PangFengYiPeiNiang", "WuBanYouZhan", "WuGuanYuWuYou", "ZhugeJunGuMai" })
            {
                var card = (CardModel)Activator.CreateInstance(mod.GetType("Yjcm." + name, true)!)!;
                Playable = true; Reason = default;
                check(Captured.ShouldGlowGold!(card), "Sly discard highlight missing for " + name);
                Playable = false; Reason = insufficientResource;
                check(Captured.ShouldGlowGold(card), "Insufficient energy must not hide the Sly discard hint for " + name);
                Reason = blocked;
                check(!Captured.ShouldGlowGold(card), "A prohibited Sly play must follow the native highlight rule for " + name);
            }
            var normal = new StrikeIronclad();
            Playable = true; Reason = default;
            check(!Captured.ShouldGlowGold!(normal), "Normal cards must not receive the Sly discard hint");
            typeof(AbstractModel).GetProperty("IsMutable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(normal, true);
            normal.GiveSingleTurnSly();
            check(Captured.ShouldGlowGold(normal), "Temporary Sly must receive the same discard hint");
            normal.EndOfTurnCleanup();
            check(!Captured.ShouldGlowGold(normal), "Expired temporary Sly must stop glowing");
            await CardSelectCmd.FromHand(null!, null!, prefs, null, null!);
            check(Captured.ShouldGlowGold == null, "Normal selection must not inherit the discard-only hint");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool Select(CardSelectorPrefs prefs, ref Task<IEnumerable<CardModel>> __result)
    { Captured = prefs; __result = Task.FromResult(Enumerable.Empty<CardModel>()); return false; }
    private static bool CanPlay(out UnplayableReason reason, out AbstractModel? preventer, ref bool __result)
    { reason = Reason; preventer = null; __result = Playable; return false; }
}
