using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Yjcm;

[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.EnergyIconPath), MethodType.Getter)]
internal static class WeiCaoCaoCardEnergyIconPatch
{
    private static void Postfix(CardPoolModel __instance, ref string __result)
    {
        if (__instance is WeiCaoCaoCardPool)
            __result = "res://Experimental/WeiCaoCao/images/weicaocao_wuque_card.png";
    }
}

[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter._Ready))]
internal static class WeiCaoCaoEnergyCounterPatch
{
    private const string HudNodeName = "YjcmCaoCaoEnergy";

    private static void Postfix(NEnergyCounter __instance, Player ____player)
    {
        if (____player.Character is not WeiCaoCao) return;

        var layers = __instance.GetNodeOrNull<Control>("%Layers");
        if (layers == null || layers.HasNode(HudNodeName)) return;

        var texture = GD.Load<Texture2D>("res://Experimental/WeiCaoCao/images/weicaocao_wuque_hud.png");
        if (texture == null)
        {
            MainFile.Logger.Warn("乌鹊南飞能量图标加载失败。");
            return;
        }

        var icon = new TextureRect
        {
            Name = HudNodeName,
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
