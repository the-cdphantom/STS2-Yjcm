using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Yjcm.Extensions;

namespace Yjcm;

/// <summary>
/// 三国杀 Mod 卡牌基类：统一处理卡面路径
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool), Inherit = true)]
public abstract class WuZhugeliangCard(int cost, CardType type, CardRarity rarity, TargetType target,
    bool shouldShowInCardLibrary = true) :
    ModCardTemplate(cost, type, rarity, target, shouldShowInCardLibrary)
{
    // 卡面路径：RitsuLib ID 去前缀后直接小写
    public override string CustomPortraitPath
    {
        get
        {
            var entry = Id.Entry;
            // RitsuLib 格式: "YJCM_CARD_HUANG_WUDIE_SHUANG_RUI" → "huang_wudie_shuang_rui.png"
            var prefix = "YJCM_CARD_";
            var name = entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? entry.Substring(prefix.Length) : entry;
            var path = $"{name.ToLowerInvariant()}.png".CardImagePath();
            return ResourceLoader.Exists(path) ? path : "card.png".CardImagePath();
        }
    }

    // 内部助手：供 ExtraHoverTips 补丁读取武将势力悬浮框
    // （ModCardTemplate.get_ExtraHoverTips 是 sealed 无法覆写，故用 Harmony 后置补丁追加）
    internal bool TryGetWuJiangHoverTip(out IHoverTip tip)
    {
        tip = null!;
        if (!CanonicalTags.Contains(YjcmCardTags.WuJiang)) return false;
        tip = WuJiangHoverTip.ForCard(CanonicalTags);
        return true;
    }
}
