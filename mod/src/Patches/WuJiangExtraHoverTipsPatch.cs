using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using STS2RitsuLib.Scaffolding.Content;

namespace Yjcm;

/// <summary>
/// 武将牌统一追加"这是X的武将"悬浮框。
///
/// ModCardTemplate.get_ExtraHoverTips 是 sealed 覆写（返回 AdditionalHoverTips），
/// 无法在基类里再覆写，因此用 Harmony 后置补丁在返回结果末尾追加势力武将提示。
/// 只对带武将 tag 的牌生效；仅含武将关键词的牌（如北伐/借东风）由自身 AdditionalHoverTips 手动处理。
/// 补丁由 MainFile 中的 <c>harmony.PatchAll()</c> 自动应用。
/// </summary>
[HarmonyPatch(typeof(ModCardTemplate), "get_ExtraHoverTips")]
public static class WuJiangExtraHoverTipsPatch
{
    public static void Postfix(ModCardTemplate __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is WuZhugeliangCard wc && wc.TryGetWuJiangHoverTip(out var tip))
            __result = __result.Concat([tip]).ToList();
        // Design cards supply their own faction tip. Only append to the copied god cards here.
        else if (__instance is WeiCaoCaoCard and not WeiCaoCaoDesignCard
                 && __instance.Tags.Contains(YjcmCardTags.WuJiang)
                 && __instance.Tags.Contains(YjcmCardTags.ShenJiang))
            __result = __result.Concat([WuJiangHoverTip.ForCard(__instance.Tags)]).ToList();
    }
}
