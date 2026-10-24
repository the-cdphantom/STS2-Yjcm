using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;

#nullable enable

namespace Yjcm;

// Native merchant visuals are ordered like _players after its local-player-first reorder.
// Refresh only our characters, in the same frame, without replacing the room or its interactions.
[HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
internal static class PlayerSkinMerchantPatch
{
    private static readonly FieldInfo PlayersField = AccessTools.Field(typeof(NMerchantRoom), "_players")
        ?? throw new MissingFieldException(typeof(NMerchantRoom).FullName, "_players");

    private static void Postfix(NMerchantRoom __instance)
    {
        if (PlayersField.GetValue(__instance) is not List<Player> players) return;
        for (int i = 0; i < players.Count && i < __instance.PlayerVisuals.Count; i++)
        {
            Player player = players[i];
            Node visual = __instance.PlayerVisuals[i];
            if (player.Character is WeiDongZhuo)
            {
                WeiDongZhuo.ConfigureVisualSkin(visual, PlayerSkins.ForPlayer(player) == WuZhugeliangSettings.TaiShanBengShiSkin);
                Node? body = visual.GetNodeOrNull("Visuals");
                if (body?.IsNodeReady() == true) body.Call("apply_skin");
            }
            else if (player.Character is WuZhugeliang &&
                     PlayerSkins.ForPlayer(player) == WuZhugeliangSettings.MouZhugeliangSkin &&
                     visual.GetNodeOrNull<CanvasItem>("Visuals") is { } original)
            {
                Node2D replacement = WuZhugeliangSkinVisuals.CreateCombatCharacter();
                original.Visible = false;
                visual.AddChild(replacement);
            }
        }
    }
}
