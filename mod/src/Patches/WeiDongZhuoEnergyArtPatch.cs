using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Yjcm;

// 威董卓能量图标采用史料元素方案 C「两剑」。
// 与武诸葛亮奇门八阵图同一接入方式：卡牌费用图标、正文内嵌能量图标（energyPrefix=yjcm_dongzhuo）、战斗 HUD 能量计数器。
internal static class WeiDongZhuoEnergyArt
{
    internal const string CardPath = "res://Yjcm/Images/ui/energy/weidongzhuo_skull_card.png";
    internal const string HudPath = "res://Yjcm/Images/ui/energy/weidongzhuo_skull_hud.png";
    internal const string HudNodeName = "YjcmDongZhuoEnergy";
}

// 只替换威董卓卡池；原生费用数字、升级/减费颜色与其他卡池不变。
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.EnergyIconPath), MethodType.Getter)]
internal static class WeiDongZhuoCardEnergyIconPatch
{
    private static void Postfix(CardPoolModel __instance, ref string __result)
    {
        if (__instance is WeiDongZhuoCardPool)
            __result = WeiDongZhuoEnergyArt.CardPath;
    }
}

[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter._Ready))]
internal static class WeiDongZhuoEnergyCounterPatch
{
    private static void Postfix(NEnergyCounter __instance, Player ____player)
    {
        if (____player.Character is not WeiDongZhuo) return;
        var layers = __instance.GetNodeOrNull<Control>("%Layers");
        if (layers == null || layers.HasNode(WeiDongZhuoEnergyArt.HudNodeName)) return;
        var texture = GD.Load<Texture2D>(WeiDongZhuoEnergyArt.HudPath);
        if (texture == null)
        {
            MainFile.Logger.Warn("威董卓能量图标加载失败，保留原生显示。");
            return;
        }

        // 保留原生容器、标签、粒子与刷新逻辑；新图加入 Layers，零能量变暗继续生效。
        var icon = new TextureRect
        {
            Name = WeiDongZhuoEnergyArt.HudNodeName,
            Texture = texture,
            Material = layers.GetNodeOrNull<Control>("Layer1")?.Material,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        foreach (var oldLayer in layers.GetChildren().OfType<CanvasItem>())
            oldLayer.Hide();
        layers.AddChild(icon);
        icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }
}
