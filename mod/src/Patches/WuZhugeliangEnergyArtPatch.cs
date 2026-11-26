using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Yjcm;

internal static class WuZhugeliangEnergyArt
{
    internal const string CardPath = "res://Yjcm/Images/ui/energy/qimen_bazhentu_card.png";
    internal const string HudPath = "res://Yjcm/Images/ui/energy/qimen_bazhentu_hud.png";
}

// Keep the native energy prefix, text formatting and all other card pools intact.
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.EnergyIconPath), MethodType.Getter)]
internal static class WuZhugeliangCardEnergyIconPatch
{
    private static void Postfix(CardPoolModel __instance, ref string __result)
    {
        if (__instance is WuZhugeliangCardPool)
            __result = WuZhugeliangEnergyArt.CardPath;
    }
}

[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter._Ready))]
internal static class WuZhugeliangEnergyCounterPatch
{
    private static void Postfix(NEnergyCounter __instance, Player ____player)
    {
        if (____player.Character is not WuZhugeliang) return;
        var layers = __instance.GetNodeOrNull<Control>("%Layers");
        if (layers == null || layers.HasNode("YjcmQiMenEnergy")) return;
        var texture = GD.Load<Texture2D>(WuZhugeliangEnergyArt.HudPath);
        if (texture == null)
        {
            MainFile.Logger.Warn("奇门八阵图能量图标加载失败，保留原生显示。");
            return;
        }

        // Leave the native container, label, particles and refresh hooks in place.
        // The new art joins Layers so zero-energy dimming continues to apply.
        var icon = new TextureRect
        {
            Name = "YjcmQiMenEnergy",
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
