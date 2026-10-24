using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

#nullable enable

namespace Yjcm;

// Cosmetics only: no character/model IDs, gameplay properties or local settings are changed on receipt.
public sealed class PlayerSkinSelection
{
    public string WuZhugeliang { get; set; } = "classic";
    public string WeiDongZhuo { get; set; } = "classic";
}

internal static class PlayerSkins
{
    internal const string MouSkinMeta = "yjcm_mou_zhugeliang_skin";
    internal static readonly PlayerRunSavedData<PlayerSkinSelection> Selections =
        RunSavedDataStore.For("Yjcm").RegisterPerPlayer<PlayerSkinSelection>(
            "character_skin", options: new RunSavedDataOptions
            {
                SyncLobbyOnChange = false,
                WritePolicy = RunSavedDataWritePolicy.AlwaysWhenRegistered
            });
    private static WeakReference<StartRunLobby>? _activeLobby;
    private static bool _registered;

    internal static void Register()
    {
        if (_registered) return;
        RitsuLibFramework.SubscribeLifecycle<RunSavedDataLobbyStagingEvent>(evt => StageLocal(evt.Lobby), false);
        RitsuLibFramework.SubscribeLifecycle<RunSavedDataPreparingEvent>(evt =>
        {
            // Multiplayer must use the host's merged per-player contributions, never host preferences for peers.
            if (evt.IsMultiplayer) return;
            foreach (Player player in evt.RunState.Players)
                if (!Selections.TryGet(evt.RunState, player.NetId, out _))
                    Selections.Set(evt.RunState, player.NetId, LocalSelection());
        }, false);
        _registered = true;
    }

    private static PlayerSkinSelection LocalSelection() => new()
    {
        WuZhugeliang = WuZhugeliangSettings.CurrentWuZhugeliangSkin,
        WeiDongZhuo = WuZhugeliangSettings.CurrentWeiDongZhuoSkin
    };

    internal static void StageLocal(StartRunLobby lobby)
    {
        _activeLobby = new(lobby);
        ulong id = lobby.NetService.NetId;
        if (!lobby.Players.Any(player => player.id == id)) return;
        // Sync is explicit: staging notifications can also originate from a received contribution.
        // Auto-pushing here would recursively send character-change messages.
        Selections.Lobby.Set(lobby, id, LocalSelection());
    }

    internal static void PublishLocalChange()
    {
        if (_activeLobby?.TryGetTarget(out StartRunLobby? lobby) != true) return;
        StageLocal(lobby);
        RunSavedDataLobby.TryPushContribution(lobby);
    }

    internal static void ForgetLobby(StartRunLobby lobby)
    {
        if (_activeLobby?.TryGetTarget(out StartRunLobby? active) == true && ReferenceEquals(active, lobby))
            _activeLobby = null;
    }

    internal static string Resolve(Type? characterType, PlayerSkinSelection? selection, bool isLocal)
    {
        if (characterType == typeof(WuZhugeliang))
        {
            string? value = selection?.WuZhugeliang;
            if (selection == null && isLocal) value = WuZhugeliangSettings.CurrentWuZhugeliangSkin;
            return value == WuZhugeliangSettings.MouZhugeliangSkin ? value : "classic";
        }
        if (characterType == typeof(WeiDongZhuo))
        {
            string? value = selection?.WeiDongZhuo;
            if (selection == null && isLocal) value = WuZhugeliangSettings.CurrentWeiDongZhuoSkin;
            return WuZhugeliangSettings.NormalizeWeiDongZhuoSkin(value!);
        }
        return "classic";
    }

    internal static string ForPlayer(Player player)
    {
        PlayerSkinSelection? selection = null;
        if (player.RunState is RunState run)
            Selections.TryGet(run, player.NetId, out selection);
        return Resolve(player.Character.GetType(), selection, LocalContext.IsMe(player));
    }
}

// Run before RitsuLib flushes the ready message's contribution trailer, including a last-second toggle.
[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.SetReady))]
internal static class PlayerSkinReadyPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(StartRunLobby __instance) => PlayerSkins.StageLocal(__instance);
}

[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.CleanUp))]
internal static class PlayerSkinLobbyCleanupPatch
{
    private static void Postfix(StartRunLobby __instance) => PlayerSkins.ForgetLobby(__instance);
}

// CharacterModel is shared by everyone using the same character. Bind cosmetics to the created node,
// not the model or SceneTree root; children run _Ready only after multiple visuals may have been created.
[HarmonyPatch(typeof(Creature), nameof(Creature.CreateVisuals))]
internal static class PlayerSkinVisualsPatch
{
    private static void Postfix(Creature __instance, NCreatureVisuals? __result)
    {
        if (__result == null || __instance.Player is not { } player) return;
        if (player.Character is WuZhugeliang)
            __result.SetMeta(PlayerSkins.MouSkinMeta, PlayerSkins.ForPlayer(player) == WuZhugeliangSettings.MouZhugeliangSkin);
        else if (player.Character is WeiDongZhuo)
            WeiDongZhuo.ConfigureVisualSkin(__result, PlayerSkins.ForPlayer(player) == WuZhugeliangSettings.TaiShanBengShiSkin);
    }
}
