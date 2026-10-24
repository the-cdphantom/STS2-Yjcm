using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Yjcm;

/// <summary>
/// Ritsu 的场景工厂只注册 NCreatureVisuals，因此在标准角色节点就绪后挂载
/// 武诸葛的 Laya 网格动画。场景根名称用于限制补丁，不影响其他角色。
/// </summary>
[HarmonyPatch(typeof(NCreatureVisuals), "_Ready")]
internal static class WuZhugeliangMeshVisualsPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreatureVisuals __instance)
    {
        if (!string.Equals(__instance.Name, "WuZhugeliang", StringComparison.OrdinalIgnoreCase))
            return;

        Node2D? host = __instance.GetNodeOrNull<Node2D>("%Visuals")
            ?? __instance.GetNodeOrNull<Node2D>("Visuals");
        if (host == null || host.GetNodeOrNull("WuZhugeliangMeshAnimation") != null ||
            host.GetNodeOrNull("MouZhugeliangCombatSpine") != null)
            return;

        try
        {
            if (__instance.GetMeta(PlayerSkins.MouSkinMeta, false).AsBool())
            {
                host.AddChild(WuZhugeliangSkinVisuals.CreateCombatCharacter());
                MainFile.Logger.Info("谋诸葛亮纯人物 Spine 战斗立绘已启用（已剔除火焰、气浪和背景部件）。");
                return;
            }

            host.AddChild(new WuZhugeliangMeshAnimation
            {
                Name = "WuZhugeliangMeshAnimation",
                Position = new Vector2(130f, -77f),
                Scale = new Vector2(0.65f, 0.65f)
            });
            MainFile.Logger.Info("武诸葛 Laya 网格动画已启用（play/play2，单图集实时渲染）。");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"武诸葛网格动画加载失败：{ex}");
        }
    }
}
