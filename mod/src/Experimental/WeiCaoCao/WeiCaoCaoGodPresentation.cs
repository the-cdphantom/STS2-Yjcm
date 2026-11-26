using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace Yjcm;

// Reuse the existing god-general presentation without making the release build
// depend on developer-only Cao Cao models.
[HarmonyPatch(typeof(CardEffectVfx), nameof(CardEffectVfx.Resolve))]
internal static class WeiCaoCaoGodEffects
{
    private static readonly string EffectRoot = Path.Combine(
        Path.GetDirectoryName(typeof(WeiCaoCaoGodEffects).Assembly.Location)!, "Spine", "WeiCaoCao", "CardEffects");

    private static bool Prefix(ref string typeName, CardType type, ref (string Folder, string Animation, string Root)? __result)
    {
        var folder = typeName switch
        {
            nameof(WeiCaoCaoMouGuoJiaXianMouYang) => "谋郭嘉阳",
            nameof(WeiCaoCaoMouGuoJiaXianMouYin) => "谋郭嘉阴",
            nameof(WeiCaoCaoMouHuangQuanQiaoDui) => "谋黄权",
            nameof(WeiCaoCaoZhouXuanWuMei) => "周宣",
            nameof(WeiCaoCaoXiaHouXuanBoXuan) => "夏侯玄",
            nameof(WeiCaoCaoShenDengAiTuoYu) => "神邓艾",
            _ => null
        };
        if (folder != null)
        {
            __result = CardEffectVfx.ResolveClip(folder, type, EffectRoot);
            return false;
        }

        if (typeName.StartsWith("WeiCaoCao", StringComparison.Ordinal)) typeName = typeName[9..];
        return true;
    }
}

[HarmonyPatch(typeof(LieCangBodySelectionPatch), "OptionNumber")]
internal static class WeiCaoCaoGodBodySelection
{
    private static void Postfix(CardModel card, ref int __result)
    {
        if (__result != 0) return;
        __result = card switch
        {
            WeiCaoCaoLieCangOption1 => 1,
            WeiCaoCaoLieCangOption2 => 2,
            WeiCaoCaoLieCangOption3 => 3,
            WeiCaoCaoLieCangOption4 => 4,
            WeiCaoCaoLieCangOption5 => 5,
            _ => 0
        };
    }
}

[HarmonyPatch(typeof(ShenCaiJudgmentPower), "SyncCardVariables")]
internal static class WeiCaoCaoGodJudgmentDisplay
{
    private static void Postfix(Player player, int count)
    {
        foreach (var pileType in new[] { PileType.Draw, PileType.Hand, PileType.Discard, PileType.Exhaust, PileType.Play })
        {
            var pile = PileTypeExtensions.GetPile(pileType, player);
            if (pile?.Cards == null) continue;
            foreach (var card in pile.Cards.OfType<WeiCaoCaoShenZhangFeiShenCai>())
                card.SetJudgmentCount(count);
        }
    }
}
